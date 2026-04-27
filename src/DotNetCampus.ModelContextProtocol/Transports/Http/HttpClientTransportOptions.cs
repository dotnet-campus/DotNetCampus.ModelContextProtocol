using DotNetCampus.ModelContextProtocol.Protocol;

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

    /// <summary>
    /// 获取或设置 initialize 时优先声明的协议版本。默认使用当前最新版本。
    /// </summary>
    public string PreferredProtocolVersion { get; init; } = ProtocolVersion.Current;

    /// <summary>
    /// 获取或设置客户端可接受的协议版本集合。默认使用当前实现支持的 Streamable HTTP 版本集合。
    /// </summary>
    public IReadOnlyList<string>? SupportedProtocolVersions { get; init; }
}
