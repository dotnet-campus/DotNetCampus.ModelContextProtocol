using System.Collections.Concurrent;
using dotnetCampus.Ipc.Pipes;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Ipc;

/// <summary>
/// DotNetCampus.Ipc 传输层的一个会话。
/// </summary>
public class IpcServerTransportSession : IServerTransportSession
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = [];
    private PeerProxy? _peer;

    /// <summary>
    /// 创建 DotNetCampus.Ipc 传输层的一个会话。
    /// </summary>
    /// <param name="sessionId">会话 Id。</param>
    public IpcServerTransportSession(string sessionId)
    {
        SessionId = sessionId;
    }

    /// <summary>
    /// DotNetCampus.Ipc 传输层其实是严格一对一对应一个 <see cref="PeerProxy"/> 的，所以其实不需要设置此属性。不过我们还是设了，调试稍微方便一点点。
    /// </summary>
    public string SessionId { get; }

    /// <inheritdoc />
    public ClientCapabilities? ConnectedClientCapabilities { get; set; }

    /// <summary>
    /// 设置与此会话关联的 IPC 对端代理，用于 SendRequestAsync 发送消息。
    /// </summary>
    internal void SetPeer(PeerProxy peer)
    {
        _peer = peer;
    }

    /// <inheritdoc />
    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
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

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        foreach (var (_, tcs) in _pendingRequests)
        {
            tcs.TrySetCanceled();
        }
        _pendingRequests.Clear();
        return ValueTask.CompletedTask;
    }
}
