using System.Collections.Concurrent;
using dotnetCampus.Ipc.Context;
using dotnetCampus.Ipc.Messages;
using dotnetCampus.Ipc.Pipes;
using dotnetCampus.Ipc.Utils.Extensions;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Ipc;

/// <summary>
/// DotNetCampus.Ipc 传输层，用于 DotNetCampus.Ipc 进行跨进程（也可以是进程内）MCP 通信。
/// </summary>
public class IpcServerTransport : IServerTransport
{
    private const ulong McpIpcHeader = 0x22CDFD581663B8F4;
    private readonly IServerTransportManager _manager;
    private readonly TaskCompletionSource _taskCompletionSource = new();
    private readonly IpcProvider _server;
    private readonly bool _isExternalIpcProvider;
    private readonly ConcurrentDictionary<string, IpcServerTransportSession> _sessions = [];

    /// <summary>
    /// 初始化 <see cref="IpcServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="ipcProvider">复用外部创建的 <see cref="IpcProvider"/>。</param>
    public IpcServerTransport(IServerTransportManager manager, IpcProvider ipcProvider)
    {
        _manager = manager;
        _server = ipcProvider;
        _isExternalIpcProvider = true;
    }

    /// <summary>
    /// 初始化 <see cref="IpcServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="pipeName">本地服务名，将作为管道名，管道服务端名</param>
    /// <param name="ipcConfiguration"></param>
    public IpcServerTransport(IServerTransportManager manager, string pipeName, IpcConfiguration? ipcConfiguration = null)
    {
        _manager = manager;
        _server = new IpcProvider(pipeName, ipcConfiguration);
        _isExternalIpcProvider = false;
    }

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public Task<Task> StartAsync(CancellationToken startingCancellationToken, CancellationToken runningCancellationToken)
    {
        Log.Info($"[McpServer][Ipc] Starting DotNetCampus.Ipc server transport.");

        _server.StartServer();
        _server.PeerConnected += OnPeerConnected;

        runningCancellationToken.Register(() => _taskCompletionSource.TrySetResult());
        return Task.FromResult<Task>(_taskCompletionSource.Task);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Log.Info($"[McpServer][Ipc] Disposing IpcServerTransport");

        if (!_isExternalIpcProvider)
        {
            _server.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private void OnPeerConnected(object? sender, PeerConnectedArgs e)
    {
        _sessions[e.Peer.PeerName] = new IpcServerTransportSession(e.Peer.PeerName);
        e.Peer.PeerConnectionBroken += OnPeerConnectionBroken;
        e.Peer.PeerReconnected += OnPeerReconnected;
        e.Peer.MessageReceived += OnMessageReceived;
    }

    private void OnPeerConnectionBroken(object? sender, IPeerConnectionBrokenArgs e)
    {
        var peer = (PeerProxy)sender!;
        _sessions.TryRemove(peer.PeerName, out _);
    }

    private void OnPeerReconnected(object? sender, IPeerReconnectedArgs e)
    {
        var peer = (PeerProxy)sender!;
        _sessions[peer.PeerName] = new IpcServerTransportSession(peer.PeerName);
    }

    private void OnMessageReceived(object? sender, IPeerMessageArgs e)
    {
        _ = OnMessageReceivedCore((PeerProxy)sender!, e.Message);

        async Task OnMessageReceivedCore(PeerProxy peer, IpcMessage message)
        {
            try
            {
                await HandleMessageAsync(peer, message);
            }
            catch (Exception ex)
            {
                Log.Error($"[McpServer][Ipc] 在处理 IPC 对等消息时发生错误。", ex);
            }
        }
    }

    private async Task HandleMessageAsync(PeerProxy peer, IpcMessage message)
    {
        if (!message.TryGetPayload(McpIpcHeader, out var payload))
        {
            // 非 MCP 的 IPC 消息（这是因为来自外部的 IpcProvider 同时还会收发其他类型的消息）。
            return;
        }

        var request = await _manager.ParseAndCatchRequestAsync(payload.Body.ToMemoryStream());
        if (request is null)
        {
            await _manager.RespondJsonRpcAsync(peer, new JsonRpcResponse
            {
                Error = new JsonRpcError
                {
                    Code = (int)JsonRpcErrorCode.InvalidRequest,
                    Message = "Invalid request message.",
                },
            }, CancellationToken.None);
            return;
        }

        var response = await _manager.HandleRequestAsync(request, null, CancellationToken.None);
        if (response is null)
        {
            // 按照 MCP 协议规范，本次请求仅需响应而无需回复。
            // 而 IPC 不需要响应。
            return;
        }

        await _manager.RespondJsonRpcAsync(peer, response, CancellationToken.None);
    }
}

file static class Extensions
{
    extension(IServerTransportManager manager)
    {
        public async ValueTask<JsonRpcRequest?> ParseAndCatchRequestAsync(Stream data)
        {
            try
            {
                return await manager.ReadRequestAsync(data);
            }
            catch
            {
                // 请求消息格式不正确，返回 null 后，原样给 MCP 客户端报告错误。
                return null;
            }
        }

        public async ValueTask RespondJsonRpcAsync(PeerProxy peer, JsonRpcResponse response, CancellationToken cancellationToken)
        {
            try
            {
                using var ms = new MemoryStream();
                await manager.WriteMessageAsync(ms, response, cancellationToken);
                await peer.NotifyAsync(new IpcMessage("", new IpcMessageBody(ms.GetBuffer(), 0, (int)ms.Length)));
            }
            catch
            {
                // 可能目标客户端已退出，重定向的流无法写入。
            }
        }
    }
}
