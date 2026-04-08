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
    /// 连接的客户端所声明的客户端能力。在 Initialize 握手完成后设置。<br/>
    /// The client capabilities declared by the connected client. Set after the Initialize handshake completes.
    /// </summary>
    ClientCapabilities? ConnectedClientCapabilities { get; set; }

    /// <summary>
    /// 向客户端发送 JSON-RPC 请求并等待响应。用于服务器主动发起的请求（如 sampling/createMessage）。<br/>
    /// Sends a JSON-RPC request to the client and waits for the response. Used for server-initiated requests (e.g. sampling/createMessage).
    /// </summary>
    Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理从客户端收到的 JSON-RPC 响应（对服务器发起的请求的回复）。<br/>
    /// Handles a JSON-RPC response received from the client (a reply to a server-initiated request).
    /// </summary>
    void HandleResponseAsync(JsonRpcResponse response);
}
