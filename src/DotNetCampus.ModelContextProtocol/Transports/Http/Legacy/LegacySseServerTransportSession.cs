using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Http.Legacy;

/// <summary>
/// 2024-11-05 HTTP+SSE 传输层会话。
/// </summary>
public sealed class LegacySseServerTransportSession : ServerTransportSession
{
    private static readonly ReadOnlyMemory<byte> EventEndpointBytes = "event: endpoint\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> EventMessageBytes = "event: message\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> DataPrefixBytes = "data: "u8.ToArray();
    private static readonly ReadOnlyMemory<byte> NewLineBytes = "\n"u8.ToArray();

    private readonly IServerTransportManager _manager;
    private readonly string _logPrefix;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();

    private Stream? _sseStream;

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public override string SessionId { get; }

    /// <summary>
    /// 创建一个 2024-11-05 HTTP+SSE 传输层会话。
    /// </summary>
    public LegacySseServerTransportSession(IServerTransportManager manager, string sessionId, string logPrefix)
    {
        _manager = manager;
        SessionId = sessionId;
        _logPrefix = logPrefix;
    }

    /// <summary>
    /// 绑定当前会话的 SSE 输出流。
    /// </summary>
    public void AttachSseStream(Stream stream)
    {
        if (_disposeCts.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(LegacySseServerTransportSession), "当前会话已被释放，无法绑定 SSE 输出流。");
        }
        _sseStream = stream;
    }

    /// <summary>
    /// 当前是否存在可用的 SSE 输出流。
    /// </summary>
    public bool HasActiveSseStream => _sseStream is not null;

    /// <summary>
    /// 清除当前会话的 SSE 输出流。
    /// </summary>
    public void DetachSseStream(Stream stream)
    {
        if (ReferenceEquals(_sseStream, stream))
        {
            _sseStream = null;
        }
    }

    /// <summary>
    /// 向客户端发送 endpoint 事件。
    /// </summary>
    public Task WriteEndpointEventAsync(Stream stream, string endpoint, CancellationToken cancellationToken)
    {
        return WriteSseEventAsync(stream, EventEndpointBytes, endpoint, cancellationToken);
    }

    /// <summary>
    /// 向当前连接发送 endpoint 事件。
    /// </summary>
    public Task WriteEndpointEventAsync(string endpoint, CancellationToken cancellationToken)
    {
        var stream = _sseStream
                     ?? throw new InvalidOperationException("当前未建立 legacy SSE 连接，无法发送 endpoint 事件。");
        return WriteEndpointEventAsync(stream, endpoint, cancellationToken);
    }

    /// <summary>
    /// 向客户端发送 JSON-RPC message 事件。
    /// </summary>
    public Task WriteMessageEventAsync(Stream stream, JsonRpcMessage message, CancellationToken cancellationToken)
    {
        return WriteSseEventAsync(stream, EventMessageBytes, message, cancellationToken);
    }

    /// <summary>
    /// 向当前连接发送 JSON-RPC message 事件。
    /// </summary>
    public Task WriteMessageEventAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        var stream = _sseStream
                     ?? throw new InvalidOperationException("当前未建立 legacy SSE 连接，无法发送 message 事件。");
        return WriteMessageEventAsync(stream, message, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task SendRequestMessageAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var stream = _sseStream
                     ?? throw new InvalidOperationException("当前未建立 legacy SSE 连接，无法发送服务端请求。");
        return WriteMessageEventAsync(stream, request, cancellationToken);
    }

    /// <inheritdoc />
    protected override void OnResponseReceived(string id, JsonRpcResponse response)
    {
        Log.Debug($"{_logPrefix} Received client response for pending legacy request. Id={id}, SessionId={SessionId}");
    }

    /// <inheritdoc />
    protected override void OnUnmatchedResponse(string id, JsonRpcResponse response)
    {
        Log.Warn($"{_logPrefix} Received unmatched client response on legacy SSE session. Id={id}, SessionId={SessionId}");
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
        _sseStream = null;
        _disposeCts.Dispose();
        _writeLock.Dispose();
    }

    private async Task WriteSseEventAsync(Stream stream, ReadOnlyMemory<byte> eventHeader, string textPayload, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(eventHeader, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(DataPrefixBytes, cancellationToken).ConfigureAwait(false);
            await using (var writer = new StreamWriter(stream, leaveOpen: true))
            {
                await writer.WriteAsync(textPayload).ConfigureAwait(false);
                await writer.FlushAsync(
#if NET8_0_OR_GREATER
                    cancellationToken
#endif
                ).ConfigureAwait(false);
            }
            await stream.WriteAsync(NewLineBytes, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(NewLineBytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task WriteSseEventAsync(Stream stream, ReadOnlyMemory<byte> eventHeader, JsonRpcMessage message, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(eventHeader, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(DataPrefixBytes, cancellationToken).ConfigureAwait(false);
            await _manager.WriteMessageAsync(stream, message, cancellationToken).ConfigureAwait(false);
            _manager.LogRawOut(_logPrefix, $"Legacy/message, SessionId={SessionId}", message);
            await stream.WriteAsync(NewLineBytes, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(NewLineBytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
