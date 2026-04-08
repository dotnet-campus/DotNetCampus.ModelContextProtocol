using dotnetCampus.Ipc.Pipes;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Ipc;

/// <summary>
/// DotNetCampus.Ipc 传输层的一个会话。
/// </summary>
public class IpcServerTransportSession : ServerTransportSession
{
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
    public override string SessionId { get; }

    /// <summary>
    /// 设置与此会话关联的 IPC 对端代理，用于 SendRequestAsync 发送消息。
    /// </summary>
    internal void SetPeer(PeerProxy peer)
    {
        _peer = peer;
    }

    /// <inheritdoc />
    protected override Task SendRequestMessageAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        // IPC 传输层的服务端主动请求尚未实现。
        throw new NotImplementedException("IPC 传输层尚不支持服务端主动发起请求（如 sampling/createMessage）。");
    }

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        CancelAllPendingRequests();
        return ValueTask.CompletedTask;
    }
}
