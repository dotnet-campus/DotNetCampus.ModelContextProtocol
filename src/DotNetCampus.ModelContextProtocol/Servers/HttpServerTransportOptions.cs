namespace DotNetCampus.ModelContextProtocol.Servers;

public record HttpServerTransportOptions
{
    /// <summary>
    /// 指定用于传输的基础 URL。
    /// </summary>
    public required string BaseUrl { get; init; } = "http://localhost:5000/";
}
