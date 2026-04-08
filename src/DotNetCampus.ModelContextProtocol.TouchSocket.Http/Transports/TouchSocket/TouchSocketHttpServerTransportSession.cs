using System.Collections.Concurrent;
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
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = [];

    /// <summary>
    /// 当前 POST 请求绑定的 SSE 输出流。
    /// 非 null 时，SendRequestAsync 直接向此流写入采样请求。
    /// </summary>
    private volatile Stream? _currentRequestSseStream;

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
    }

    /// <summary>
    /// 将当前 POST 请求的 SSE 输出流绑定到此会话。
    /// 返回的 <see cref="IDisposable"/> Dispose 后自动清除绑定。
    /// </summary>
    internal IDisposable SetRequestSseStream(Stream stream)
    {
        _currentRequestSseStream = stream;
        return new SseStreamScope(this);
    }

    private void ClearRequestSseStream()
    {
        _currentRequestSseStream = null;
    }

    /// <inheritdoc />
    public async Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Id?.ToString() is not { } id)
        {
            throw new InvalidOperationException("请求 ID 不能为 null。Request ID must not be null.");
        }

        var stream = _currentRequestSseStream
            ?? throw new InvalidOperationException("当前没有绑定的 SSE 流，无法发送服务端主动请求。");

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
            // 直接写入当前 POST 请求的 SSE 流，不经过 Channel。
            await WriteSseMessageAsync(stream, request, cancellationToken).ConfigureAwait(false);
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
        foreach (var (_, tcs) in _pendingRequests)
        {
            tcs.TrySetCanceled();
        }
        _pendingRequests.Clear();
        _disposeCts.Dispose();
    }

    private sealed class SseStreamScope(TouchSocketHttpServerTransportSession session) : IDisposable
    {
        public void Dispose() => session.ClearRequestSseStream();
    }
}
