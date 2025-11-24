namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// HTTP 传输层配置选项。
/// </summary>
public record HttpServerTransportOptions
{
    /// <summary>
    /// 指定用于传输的基础 URL。
    /// </summary>
    public IReadOnlyList<string> UrlPrefixes { get; init; } =
    [
        "http://localhost:5000/",
        "http://127.0.0.1:5000/",
    ];

    /// <summary>
    /// 指定用于传输的端点。
    /// </summary>
    public string Endpoint { get; init; } = "mcp";
}
