using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using dotnetCampus.Ipc.Messages;
using dotnetCampus.Ipc.Pipes;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Ipc;

/// <summary>
/// DotNetCampus.Ipc 传输层的一个会话。
/// </summary>
public class IpcServerTransportSession : ServerTransportSession
{
    // System.Runtime.InteropServices.MemoryMarshal.Read<ulong>("Dncp.Mcp"u8).ToString("X")
    // 小端写入时，可在 IPC 传输序列中看到 Dncp.Mcp = DotNetCampus.ModelContextProtocol 的 ASCII 字符串。
    internal const ulong McpIpcHeader = 0x70634D2E70636E44;

    private readonly IServerTransportManager _manager;
    private readonly IpcProvider _ipcProvider;
    private readonly string _peerName;

    /// <summary>
    /// 创建 DotNetCampus.Ipc 传输层的一个会话。
    /// </summary>
    /// <param name="manager"></param>
    /// <param name="peerName">IPC 远程端点名，同时也是会话 Id。</param>
    /// <param name="ipcServer"></param>
    public IpcServerTransportSession(IServerTransportManager manager, IpcProvider ipcServer, string peerName)
    {
        _manager = manager;
        _ipcProvider = ipcServer;
        _peerName = peerName;
    }

    /// <summary>
    /// DotNetCampus.Ipc 传输层其实是严格一对一对应一个 <see cref="PeerProxy"/> 的，所以其实不需要设置此属性。不过我们还是设了，调试稍微方便一点点。
    /// </summary>
    public override string SessionId => _peerName;

    /// <inheritdoc />
    protected override async Task SendRequestMessageAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (_peerName is not { } peerName)
        {
            throw new InvalidOperationException("IPC 对端代理尚未设置，无法发送服务端主动请求。请确认 SetPeer 已在连接建立时被调用。");
        }

        _manager.LogRawOut("[Ipc]", request);
        using var ms = new MemoryStream();
        await _manager.WriteMessageAsync(ms, request, cancellationToken);
        var result = await _ipcProvider.TryConnectToExistingPeerAsync(peerName, true);
        if (result.IsSuccess)
        {
            await result.PeerProxy.NotifyAsync(new IpcMessage("McpServer.SendMessage", new IpcMessageBody(ms.GetBuffer(), 0, (int)ms.Length), McpIpcHeader));
        }
        else
        {
            throw new InvalidOperationException($"目标进程已退出，无法发送消息。目标端点名：{peerName}");
        }
    }

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        CancelAllPendingRequests();
        return ValueTask.CompletedTask;
    }
}
