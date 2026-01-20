namespace DotNetCampus.ModelContextProtocol.Clients.Transports;

/// <summary>
/// MCP 客户端的传输层。
/// </summary>
public interface IClientTransport : IAsyncDisposable
{
    /// <summary>
    /// 获取传输层是否已连接。
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 连接到 MCP 服务器。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>连接成功后返回。</returns>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开与 MCP 服务器的连接。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>断开连接后返回。</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
