using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// MCP 客户端的传输层。
/// </summary>
public interface IClientTransport : IAsyncDisposable
{
    /// <summary>
    /// 连接到 MCP 服务器。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>连接成功后返回。</returns>
    ValueTask ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开与 MCP 服务器的连接。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>断开连接后返回。</returns>
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 将消息发送给服务器。
    /// </summary>
    /// <param name="message">要发送给服务器的消息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>消息发送完成后返回（不会等待回应）。</returns>
    ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken);
}
