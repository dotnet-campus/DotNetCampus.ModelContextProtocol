using System.Diagnostics.CodeAnalysis;
using DotNetCampus.Logging;
using DotNetCampus.ModelContextProtocol.Core;

namespace DotNetCampus.ModelContextProtocol.Servers;

public record McpServerContext
{
    public McpServerHandlers Handlers { get; init; } = new();

    [NotNull]
    internal ILogger? Logger
    {
        get => field ??= Log.Current;
        init;
    }

    [NotNull]
    public IMcpServerToolJsonSerializer? JsonSerializer
    {
        get => field ??= new McpServerToolJsonSerializer();
        init;
    }
}
