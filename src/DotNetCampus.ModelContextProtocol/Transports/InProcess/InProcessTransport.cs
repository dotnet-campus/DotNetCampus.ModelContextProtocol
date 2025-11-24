using System.Text.Json;
using System.Threading.Channels;
using DotNetCampus.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using McpServerRequestJsonContext = DotNetCampus.ModelContextProtocol.CompilerServices.McpServerRequestJsonContext;
using McpServerResponseJsonContext = DotNetCampus.ModelContextProtocol.CompilerServices.McpServerResponseJsonContext;

namespace DotNetCampus.ModelContextProtocol.Transports.InProcess;

/// <summary>
/// InProcess 传输层实现（客户端和服务器在同一进程内）。<br/>
/// 通过内存 Channel 进行消息传递，性能最高。<br/>
/// InProcess transport implementation (client and server in the same process).<br/>
/// Messages are passed through in-memory Channel, with the highest performance.
/// </summary>
/// <remarks>
/// InProcess 传输层不涉及跨进程通信，因此不需要序列化/反序列化，性能最优。<br/>
/// 适用于测试、嵌入式场景等。
/// </remarks>
public sealed class InProcessTransport : IMcpServerTransport
{
    private readonly InProcessTransportOptions _options;
    private readonly ILogger _logger;
    private readonly Channel<TransportMessageContext> _serverToClient;
    private readonly Channel<TransportMessageContext> _clientToServer;

    /// <summary>
    /// 初始化 <see cref="InProcessTransport"/> 类的新实例。
    /// </summary>
    /// <param name="options">InProcess 传输层配置</param>
    /// <param name="logger">日志记录器</param>
    internal InProcessTransport(InProcessTransportOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
        var bufferSize = options.BufferSize;
        _serverToClient = Channel.CreateBounded<TransportMessageContext>(new BoundedChannelOptions(bufferSize)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
        _clientToServer = Channel.CreateBounded<TransportMessageContext>(new BoundedChannelOptions(bufferSize)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    /// <inheritdoc />
    public string Name => _options.Name;

    /// <summary>
    /// 服务器端消息读取器（接收客户端消息）<br/>
    /// Server-side message reader (receive messages from client)
    /// </summary>
    public ChannelReader<TransportMessageContext> MessageReader => _clientToServer.Reader;

    /// <summary>
    /// 客户端消息读取器（接收服务器消息）<br/>
    /// Client-side message reader (receive messages from server)
    /// </summary>
    public ChannelReader<TransportMessageContext> ClientMessageReader => _serverToClient.Reader;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.Info($"[{Name}] Starting InProcess transport");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(JsonRpcMessage message, ITransportContext? context = null, CancellationToken cancellationToken = default)
    {
        // InProcess 是一对一传输层，不需要 context
        // 服务器端发送消息到客户端
        await _serverToClient.Writer.WriteAsync(new TransportMessageContext(message, new InProcessTransportContext()), cancellationToken);
        _logger.Trace($"[{Name}] Server sent message: {message.GetType().Name}");
    }

    /// <summary>
    /// 客户端发送消息到服务器<br/>
    /// Client sends message to server
    /// </summary>
    /// <param name="message">JSON-RPC 消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public async Task ClientSendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        await _clientToServer.Writer.WriteAsync(new TransportMessageContext(message, new InProcessTransportContext()), cancellationToken);
        _logger.Trace($"[{Name}] Client sent message: {message.GetType().Name}");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _serverToClient.Writer.Complete();
        _clientToServer.Writer.Complete();
        _logger.Info($"[{Name}] Stopping InProcess transport");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _logger.Info($"[{Name}] Disposed");
    }
}
