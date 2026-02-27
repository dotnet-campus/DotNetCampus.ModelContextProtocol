namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// HTTP 客户端传输层配置选项。
/// </summary>
public class HttpClientTransportOptions
{
    /// <summary>
    /// 获取或设置 MCP 服务器的 URL（例如：http://localhost:5000/mcp）。
    /// </summary>
    public required string ServerUrl { get; init; }

    /// <summary>
    /// 获取或设置自定义的 HttpClient 实例。如果未设置，将创建新的 <see cref="HttpClient"/>。
    /// </summary>
    public HttpClient? HttpClient { get; init; }
}
