using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// MCP 服务器传输层接口。<br/>
/// 传输层负责在客户端和服务器之间传输 JSON-RPC 消息，不处理业务逻辑。<br/>
/// MCP server transport interface.<br/>
/// The transport layer is responsible for transmitting JSON-RPC messages between client and server, without handling business logic.
/// </summary>
public interface IMcpServerTransport : IAsyncDisposable
{
    /// <summary>
    /// 传输层名称（用于日志和诊断）<br/>
    /// Transport name (for logging and diagnostics)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 消息读取器（接收来自客户端的消息）。<br/>
    /// 传输层从底层协议读取消息后，将其写入此 Channel，业务层从此 Channel 读取消息进行处理。<br/>
    /// Message reader (receive messages from clients).<br/>
    /// The transport layer writes messages to this Channel after reading from the underlying protocol,
    /// and the business layer reads from this Channel to process messages.
    /// </summary>
    ChannelReader<TransportMessageContext> MessageReader { get; }

    /// <summary>
    /// 发送消息到客户端。<br/>
    /// 业务层调用此方法将响应消息发送给客户端，传输层负责将消息序列化并通过底层协议发送。<br/>
    /// Send a message to the client.<br/>
    /// The business layer calls this method to send response messages to the client,
    /// and the transport layer serializes and sends the message through the underlying protocol.
    /// </summary>
    /// <param name="message">要发送的 JSON-RPC 消息</param>
    /// <param name="context">传输上下文（用于多对一传输层识别客户端）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task SendMessageAsync(JsonRpcMessage message, ITransportContext? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动传输层（开始监听）<br/>
    /// Start the transport (begin listening)
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止传输层<br/>
    /// Stop the transport
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}
