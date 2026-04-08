using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// Streamable HTTP 传输层的一个会话。
/// </summary>
internal class LocalHostHttpServerTransportSession : IServerTransportSession
{
    private static readonly ReadOnlyMemory<byte> EventMessageBytes = "event: message\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> DataPrefixBytes = "data: "u8.ToArray();
    private static readonly ReadOnlyMemory<byte> NewLineBytes = "\n"u8.ToArray();

    private readonly IServerTransportManager _manager;
    private readonly Channel<JsonRpcMessage> _outgoingMessages;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = [];

    private IMcpLogger Log => _manager.Context.Logger;

    public string SessionId { get; }

    /// <inheritdoc />
    public ClientCapabilities? ConnectedClientCapabilities { get; set; }

    public LocalHostHttpServerTransportSession(IServerTransportManager manager, string sessionId)
    {
        _manager = manager;
        SessionId = sessionId;
        _outgoingMessages = Channel.CreateUnbounded<JsonRpcMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_disposeCts.IsCancellationRequested)
        {
            return Task.CompletedTask;
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

        Log.Debug($"[McpServer][StreamableHttp] Sending server-initiated request. Method={request.Method}, Id={id}, SessionId={SessionId}");

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
            // 通过 SSE 通道将请求发送给客户端。
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
            Log.Debug($"[McpServer][StreamableHttp] Received client response for pending request. Id={id}, SessionId={SessionId}");
            tcs.TrySetResult(response);
        }
        else
        {
            Log.Warn($"[McpServer][StreamableHttp] Received unmatched client response. Id={id}, SessionId={SessionId}");
        }
    }

    public async Task RunSseConnectionAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        var ct = linkedCts.Token;

        try
        {
            Log.Debug($"[McpServer][StreamableHttp] SSE connection started. SessionId={SessionId}");

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
            Log.Warn($"[McpServer][StreamableHttp] SSE connection error. SessionId={SessionId}, Error={ex.Message}");
        }
        finally
        {
            Log.Debug($"[McpServer][StreamableHttp] SSE connection ended. SessionId={SessionId}");
        }
    }

    private async Task WriteSseMessageAsync(Stream stream, JsonRpcMessage message, CancellationToken ct)
    {
        try
        {
            // event: message
            await stream.WriteAsync(EventMessageBytes, ct);

            // data: ...
            await stream.WriteAsync(DataPrefixBytes, ct);

            // Serialize
            if (Log.IsEnabled(LoggingLevel.Debug))
            {
                using var ms = new MemoryStream();
                await _manager.WriteMessageAsync(ms, message, ct);
                var json = Encoding.UTF8.GetString(ms.ToArray());
                Log.Debug($"[McpServer][StreamableHttp] → {json}");
                await stream.WriteAsync(ms.ToArray(), ct);
            }
            else
            {
                await _manager.WriteMessageAsync(stream, message, ct);
            }

            // \n\n (End of event)
            await stream.WriteAsync(NewLineBytes, ct);
            await stream.WriteAsync(NewLineBytes, ct);

            await stream.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][StreamableHttp] Failed to write SSE message. SessionId={SessionId}", ex);
            throw; // Re-throw to close connection if write fails
        }
    }

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
}
