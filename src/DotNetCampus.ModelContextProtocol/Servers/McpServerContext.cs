using System.Diagnostics.CodeAnalysis;
using DotNetCampus.Logging;
using DotNetCampus.ModelContextProtocol.Core;

namespace DotNetCampus.ModelContextProtocol.Servers;

public record McpServerContext
{
    internal McpServerContext(ILogger logger)
    {
        Logger = logger;
    }

    internal ILogger Logger { get; init; }

    public McpServerHandlers Handlers { get; init; } = new();

    [NotNull]
    public IMcpServerToolJsonSerializer? JsonSerializer
    {
        get => field ??= new McpServerToolJsonSerializer();
        init;
    }
}
