using dotnetCampus.Ipc.Pipes;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Ipc;

/// <summary>
/// DotNetCampus.Ipc 传输层的一个会话。
/// </summary>
public class IpcServerTransportSession : IServerTransportSession
{
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
    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
