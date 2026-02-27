using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// MCP 服务器传输层中，某个特定的客户端与其对应服务器之间的一对一专用全双工传输层会话。
/// </summary>
public interface IServerTransportSession : IAsyncDisposable
{
    /// <summary>
    /// 会话 ID。<br/>
    /// 在实质上一对一的传输层（如 stdio）中，该值永远为 <see langword="null"/>。<br/>
    /// 而在实质上多对一的传输层（如 http）中，该值为用于唯一区分某个客户端连接的 Id。
    /// </summary>
    string? SessionId { get; }

    /// <summary>
    /// 将消息发送给其他端。
    /// </summary>
    Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default);
}
