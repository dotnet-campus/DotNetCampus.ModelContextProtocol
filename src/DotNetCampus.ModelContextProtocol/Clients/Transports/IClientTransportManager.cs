using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Clients.Transports;

/// <summary>
/// 用于管理 MCP 客户端传输层的管理器接口。
/// </summary>
public interface IClientTransportManager
{
    /// <summary>
    /// 获取用于传输层的上下文信息。
    /// </summary>
    IClientTransportContext Context { get; }

    /// <summary>
    /// 发送请求并等待响应。
    /// </summary>
    /// <param name="request">要发送的 JSON-RPC 请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>服务器的响应。</returns>
    ValueTask<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送通知（不期望响应）。
    /// </summary>
    /// <param name="notification">要发送的 JSON-RPC 通知。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    ValueTask SendNotificationAsync(JsonRpcNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 连接到服务器。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开连接。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取是否已连接。
    /// </summary>
    bool IsConnected { get; }
}
