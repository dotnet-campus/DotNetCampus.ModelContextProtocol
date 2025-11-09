using DotNetCampus.Logging;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// Placeholder class for the MCP protocol implementation.
/// This will be replaced with actual implementation.
/// </summary>
public class McpServer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="McpServer"/> class.
    /// </summary>
    public McpServer()
    {
    }

    public static HttpServerTransport CreateHttpServerTransport(string url)
    {
        return new HttpServerTransport(new McpServerContext(Log.Current), new HttpServerTransportOptions
        {
            BaseUrl = url,
        });
    }
}
