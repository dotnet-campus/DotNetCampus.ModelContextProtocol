using DotNetCampus.Logging;

namespace DotNetCampus.ModelContextProtocol.Servers;

public record McpServerContext
{
    internal McpServerContext(ILogger logger)
    {
        Logger = logger;
    }

    internal ILogger Logger { get; init; }
}
