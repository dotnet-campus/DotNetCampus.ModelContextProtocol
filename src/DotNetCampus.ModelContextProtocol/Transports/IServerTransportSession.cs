using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
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
    /// 连接的客户端所声明的客户端能力。在 Initialize 握手完成后设置。
    /// </summary>
    ClientCapabilities? ConnectedClientCapabilities { get; set; }

    /// <summary>
    /// 当前会话协商出的协议版本。在 Initialize 握手完成后设置。
    /// </summary>
    ProtocolVersion? NegotiatedProtocolVersion { get; set; }

    /// <summary>
    /// 连接的客户端在 Initialize 握手时提供的客户端信息（名称、版本等）。在 Initialize 握手完成后设置。
    /// </summary>
    Implementation? ConnectedClientInfo { get; set; }

    /// <summary>
    /// 向客户端发送 JSON-RPC 请求并等待响应。用于服务器主动发起的请求（如 sampling/createMessage）。
    /// </summary>
    Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理从客户端收到的 JSON-RPC 响应（对服务器发起的请求的回复）。
    /// </summary>
    void HandleResponseAsync(JsonRpcResponse response);
}
