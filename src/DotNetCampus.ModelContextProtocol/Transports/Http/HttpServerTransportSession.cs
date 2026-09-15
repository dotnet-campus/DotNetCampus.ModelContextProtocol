using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// Streamable HTTP 传输层的一个会话。
/// 同时被 <see cref="LocalHostHttpServerTransport"/> 和 TouchSocket HTTP 传输层使用。
/// </summary>
public class HttpServerTransportSession : ServerTransportSession
{
    private static readonly ReadOnlyMemory<byte> EventMessageBytes = "event: message\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> DataPrefixBytes = "data: "u8.ToArray();
    private static readonly ReadOnlyMemory<byte> NewLineBytes = "\n"u8.ToArray();

    private readonly IServerTransportManager _manager;
    private readonly string _logPrefix;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// 当前 POST 请求绑定的 SSE 输出流，使用 AsyncLocal 确保每个异步执行上下文（即每个并发 POST 请求）
    /// 都拥有独立的值，避免多个并发请求相互覆盖导致竞态条件。
    /// 非 null 时，SendRequestAsync 直接向此流写入采样请求。
    /// </summary>
    private static readonly AsyncLocal<Stream?> _currentRequestSseStream = new();

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public override string SessionId { get; }

    /// <summary>
    /// 初始化 <see cref="HttpServerTransportSession"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="sessionId">唯一标识此会话的 ID。</param>
    /// <param name="logPrefix">日志前缀，用于区分不同传输层实现（如 "[McpServer][StreamableHttp]"）。</param>
    public HttpServerTransportSession(IServerTransportManager manager, string sessionId, string logPrefix)
    {
        _manager = manager;
        SessionId = sessionId;
        _logPrefix = logPrefix;
    }

    /// <summary>
    /// 将当前 POST 请求的 SSE 输出流绑定到此会话。
    /// 返回的 <see cref="IDisposable"/> Dispose 后自动清除绑定（在 POST 请求处理完成后由 Transport 调用）。
    /// </summary>
    public IDisposable SetRequestSseStream(Stream stream)
    {
        _currentRequestSseStream.Value = stream;
        return new SseStreamScope(this, stream);
    }

    private void ClearRequestSseStream(Stream stream)
    {
        if (_currentRequestSseStream.Value == stream)
        {
            _currentRequestSseStream.Value = null;
        }
    }

    /// <inheritdoc />
    protected override async Task SendRequestMessageAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var stream = _currentRequestSseStream.Value
                     ?? throw new InvalidOperationException("当前没有绑定的 SSE 流，无法发送服务端主动请求。");

        Log.Debug($"{_logPrefix} Sending server-initiated request. Method={request.Method}, Id={request.Id}, SessionId={SessionId}");

        // 直接写入当前 POST 请求的 SSE 流，不经过 Channel。
        await WriteSseMessageAsync(stream, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void OnResponseReceived(string id, JsonRpcResponse response)
    {
        Log.Debug($"{_logPrefix} Received client response for pending request. Id={id}, SessionId={SessionId}");
    }

    /// <inheritdoc />
    protected override void OnUnmatchedResponse(string id, JsonRpcResponse response)
    {
        Log.Warn($"{_logPrefix} Received unmatched client response. Id={id}, SessionId={SessionId}");
    }

    /// <summary>
    /// 将一条 JSON-RPC 消息写入 SSE 流。
    /// </summary>
    public async Task WriteSseMessageAsync(Stream stream, JsonRpcMessage message, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // event: message
            await stream.WriteAsync(EventMessageBytes, ct);

            // data: ...
            await stream.WriteAsync(DataPrefixBytes, ct);

            // Serialize
            await _manager.WriteMessageAsync(stream, message, ct);
            _manager.LogRawOut(_logPrefix, $"POST/sse, SessionId={SessionId}", message);

            // \n\n (End of event)
            await stream.WriteAsync(NewLineBytes, ct);
            await stream.WriteAsync(NewLineBytes, ct);

            await stream.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            Log.Error($"{_logPrefix} Failed to write SSE message. SessionId={SessionId}", ex);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
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
        CancelAllPendingRequests();
        _disposeCts.Dispose();
        _writeLock.Dispose();
    }

    private sealed class SseStreamScope(HttpServerTransportSession session, Stream stream) : IDisposable
    {
        public void Dispose() => session.ClearRequestSseStream(stream);
    }
}
