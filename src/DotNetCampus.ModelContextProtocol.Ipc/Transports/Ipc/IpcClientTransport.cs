using System.Text;
using dotnetCampus.Ipc.Context;
using dotnetCampus.Ipc.Messages;
using dotnetCampus.Ipc.Pipes;
using dotnetCampus.Ipc.Utils.Extensions;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Ipc;

/// <summary>
/// DotNetCampus.Ipc 客户端传输层，用于通过 IPC 连接到 MCP 服务器。
/// </summary>
public sealed class IpcClientTransport : IClientTransport
{
    private const ulong McpIpcHeader = IpcServerTransportSession.McpIpcHeader;

    private readonly IClientTransportManager _manager;
    private readonly string _serverPipeName;
    private readonly IpcConfiguration? _ipcConfiguration;
    private readonly IpcProvider? _externalIpcProvider;
    private IpcProvider? _ownedIpcProvider;
    private PeerProxy? _serverPeer;
    private int _connected;

    /// <summary>
    /// 初始化 <see cref="IpcClientTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="serverPipeName">要连接的 MCP 服务器的管道名。</param>
    /// <param name="ipcConfiguration">IPC 配置。</param>
    public IpcClientTransport(IClientTransportManager manager, string serverPipeName, IpcConfiguration? ipcConfiguration = null)
    {
        _manager = manager;
        _serverPipeName = serverPipeName;
        _ipcConfiguration = ipcConfiguration;
    }

    /// <summary>
    /// 初始化 <see cref="IpcClientTransport"/> 类的新实例（复用外部 <see cref="IpcProvider"/>）。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="ipcProvider">复用外部创建的 <see cref="IpcProvider"/>。</param>
    /// <param name="serverPipeName">要连接的 MCP 服务器的管道名。</param>
    public IpcClientTransport(IClientTransportManager manager, IpcProvider ipcProvider, string serverPipeName)
    {
        _manager = manager;
        _externalIpcProvider = ipcProvider;
        _serverPipeName = serverPipeName;
    }

    private IMcpLogger Log => _manager.Context.Logger;

    private IpcProvider IpcProvider => _externalIpcProvider ?? _ownedIpcProvider
        ?? throw new InvalidOperationException("IPC 客户端传输层尚未连接。");

    /// <inheritdoc />
    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _connected, 1, 0) != 0)
        {
            return;
        }

        try
        {
            Log.Info($"[McpClient][Ipc] Connecting to server pipe '{_serverPipeName}'.");

            if (_externalIpcProvider is null)
            {
                var clientPipeName = Guid.NewGuid().ToString("N");
                _ownedIpcProvider = new IpcProvider(clientPipeName, _ipcConfiguration);
                _ownedIpcProvider.StartServer();
            }

            var peer = await IpcProvider.GetAndConnectToPeerAsync(_serverPipeName);
            _serverPeer = peer;
            peer.MessageReceived += OnMessageReceived;

            Log.Info($"[McpClient][Ipc] Connected to server pipe '{_serverPipeName}'.");
        }
        catch
        {
            Interlocked.Exchange(ref _connected, 0);
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _connected, 0) == 0)
        {
            return ValueTask.CompletedTask;
        }

        Log.Info($"[McpClient][Ipc] Disconnecting from server.");

        if (_serverPeer is { } peer)
        {
            peer.MessageReceived -= OnMessageReceived;
            _serverPeer = null;
        }

        if (_ownedIpcProvider is { } ownedProvider)
        {
            ownedProvider.Dispose();
            _ownedIpcProvider = null;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _connected) == 0 || _serverPeer is not { } peer)
        {
            throw new InvalidOperationException("IPC 传输层尚未连接或已经断开，无法发送消息。");
        }

        using var ms = new MemoryStream();
        await _manager.WriteMessageAsync(ms, message, cancellationToken);
        _manager.LogRawOut("[Ipc]", message);
        await peer.NotifyAsync(new IpcMessage("McpClient.SendMessage", new IpcMessageBody(ms.GetBuffer(), 0, (int)ms.Length), McpIpcHeader));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    private void OnMessageReceived(object? sender, IPeerMessageArgs e)
    {
        _ = OnMessageReceivedCore(e.Message);

        async Task OnMessageReceivedCore(IpcMessage message)
        {
            try
            {
                if (!message.TryGetPayload(McpIpcHeader, out var payload))
                {
                    // 非 MCP 的 IPC 消息。
                    return;
                }

                var line = Encoding.UTF8.GetString(payload.Body.Buffer, payload.Body.Start, payload.Body.Length);
                _manager.LogRawIn("[Ipc]", line);

                JsonRpcMessage? parsed;
                try
                {
                    parsed = await _manager.ReadMessageAsync(line);
                }
                catch
                {
                    Log.Warn($"[McpClient][Ipc] Invalid server message received.");
                    return;
                }

                switch (parsed)
                {
                    case JsonRpcRequest request:
                        await _manager.HandleServerRequestAsync(request, CancellationToken.None).ConfigureAwait(false);
                        break;
                    case JsonRpcResponse response:
                        await _manager.HandleRespondAsync(response, CancellationToken.None).ConfigureAwait(false);
                        break;
                    default:
                        Log.Warn($"[McpClient][Ipc] Unrecognized server message received.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[McpClient][Ipc] Error handling server message.", ex);
            }
        }
    }
}
