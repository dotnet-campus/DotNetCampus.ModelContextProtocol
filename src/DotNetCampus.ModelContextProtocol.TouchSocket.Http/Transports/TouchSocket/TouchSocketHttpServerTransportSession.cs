using System.Collections.Concurrent;
using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;

namespace DotNetCampus.ModelContextProtocol.Transports.TouchSocket;

/// <summary>
/// Streamable HTTP 传输层的一个会话。
/// </summary>
public class TouchSocketHttpServerTransportSession : IServerTransportSession
{
    private static readonly ReadOnlyMemory<byte> EventMessageBytes = "event: message\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> DataPrefixBytes = "data: "u8.ToArray();
    private static readonly ReadOnlyMemory<byte> NewLineBytes = "\n"u8.ToArray();

    private readonly IServerTransportManager _manager;
    private readonly Channel<JsonRpcMessage> _outgoingMessages;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = [];

    /// <summary>
    /// 当前 POST 请求绑定的 per-request SSE 写入通道。
    /// 非 null 时，SendMessageAsync/SendRequestAsync 优先写入此通道（走 POST 响应 SSE 流）。
    /// null 时回退到 _outgoingMessages（走 GET SSE 流）。
    /// </summary>
    private volatile ChannelWriter<JsonRpcMessage>? _requestSseWriter;

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public string SessionId { get; }

    /// <inheritdoc />
    public ClientCapabilities? ConnectedClientCapabilities { get; set; }

    /// <summary>
    /// 初始化 <see cref="TouchSocketHttpServerTransportSession"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="sessionId">唯一标识此会话的 ID。</param>
    public TouchSocketHttpServerTransportSession(IServerTransportManager manager, string sessionId)
    {
        _manager = manager;
        SessionId = sessionId;
        _outgoingMessages = Channel.CreateUnbounded<JsonRpcMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <inheritdoc />
    public IDisposable AttachRequestSseChannel(ChannelWriter<JsonRpcMessage> writer)
    {
        _requestSseWriter = writer;
        return new RequestSseChannelRegistration(this);
    }

    private void DetachRequestSseChannel()
    {
        _requestSseWriter = null;
    }

    /// <inheritdoc />
    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_disposeCts.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }
        // 优先写入 per-request 通道（POST 响应 SSE 流）；否则走全局 GET SSE 通道。
        var writer = _requestSseWriter;
        if (writer is not null)
        {
            return writer.WriteAsync(message, cancellationToken).AsTask();
        }
        return _outgoingMessages.Writer.WriteAsync(message, cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public async Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Id?.ToString() is not { } id)
        {
            throw new InvalidOperationException("请求 ID 不能为 null。Request ID must not be null.");
        }

        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        using var registration = cancellationToken.Register(() =>
        {
            if (_pendingRequests.TryRemove(id, out var removed))
            {
                removed.TrySetCanceled(cancellationToken);
            }
        });

        try
        {
            // 通过 SSE 通道将请求发送给客户端（优先 per-request，否则 GET SSE）。
            await SendMessageAsync(request, cancellationToken).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pendingRequests.TryRemove(id, out _);
        }
    }

    /// <inheritdoc />
    public void HandleResponseAsync(JsonRpcResponse response)
    {
        if (response.Id?.ToString() is not { } id)
        {
            return;
        }

        if (_pendingRequests.TryRemove(id, out var tcs))
        {
            tcs.TrySetResult(response);
        }
    }

    /// <summary>
    /// 运行 SSE 长连接，持续向客户端推送消息，直到连接断开或取消。
    /// </summary>
    /// <param name="outputStream">用于向客户端写入 SSE 数据的输出流。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    public async Task RunSseConnectionAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        var ct = linkedCts.Token;

        try
        {
            Log.Debug($"[McpServer][TouchSocket] SSE connection started. SessionId={SessionId}");

            // Wait for messages and write them
            await foreach (var message in _outgoingMessages.Reader.ReadAllAsync(ct))
            {
                await WriteSseMessageAsync(outputStream, message, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            Log.Warn($"[McpServer][TouchSocket] SSE connection error. SessionId={SessionId}, Error={ex.Message}");
        }
        finally
        {
            Log.Debug($"[McpServer][TouchSocket] SSE connection ended. SessionId={SessionId}");
        }
    }

    /// <summary>
    /// 运行 per-request SSE 流：持续消费 <paramref name="channel"/> 中的消息并写入 <paramref name="outputStream"/>，
    /// 直到 channel 完成（Complete）或取消。
    /// </summary>
    public async Task RunRequestSseAsync(Channel<JsonRpcMessage> channel, Stream outputStream, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
            {
                await WriteSseMessageAsync(outputStream, message, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            Log.Warn($"[McpServer][TouchSocket] Per-request SSE error. SessionId={SessionId}, Error={ex.Message}");
        }
    }

    internal async Task WriteSseMessageAsync(Stream stream, JsonRpcMessage message, CancellationToken ct)
    {
        try
        {
            // event: message
            await stream.WriteAsync(EventMessageBytes, ct);

            // data: ...
            await stream.WriteAsync(DataPrefixBytes, ct);

            // Serialize
            await _manager.WriteMessageAsync(stream, message, ct);

            // \n\n (End of event)
            await stream.WriteAsync(NewLineBytes, ct);
            await stream.WriteAsync(NewLineBytes, ct);

            await stream.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][TouchSocket] Failed to write SSE message. SessionId={SessionId}", ex);
            throw; // Re-throw to close connection if write fails
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposeCts.IsCancellationRequested)
        {
            return;
        }

#if NET8_0_OR_GREATER
        await _disposeCts.CancelAsync();
#else
        await Task.Yield();
        _disposeCts.Cancel();
#endif
        _outgoingMessages.Writer.TryComplete();
        foreach (var (_, tcs) in _pendingRequests)
        {
            tcs.TrySetCanceled();
        }
        _pendingRequests.Clear();
        _disposeCts.Dispose();
    }

    private sealed class RequestSseChannelRegistration(TouchSocketHttpServerTransportSession session) : IDisposable
    {
        public void Dispose() => session.DetachRequestSseChannel();
    }
}
