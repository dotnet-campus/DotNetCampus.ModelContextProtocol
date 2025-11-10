namespace DotNetCampus.ModelContextProtocol.Servers;

public record HttpServerTransportOptions
{
    /// <summary>
    /// 指定用于传输的基础 URL。
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:5000/";

    /// <summary>
    /// 指定用于传输的端点。
    /// </summary>
    public string Endpoint { get; init; } = "mcp";
}
