using System.Diagnostics.CodeAnalysis;
using DotNetCampus.Logging;
using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Servers;

public record McpServerContext
{
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

    [NotNull]
    internal McpRequestHandlerRegistry? Handlers
    {
        get => field ?? throw new InvalidOperationException("Handlers 未被设置。");
        set => field = field switch
        {
            null => value,
            _ => throw new InvalidOperationException("Handlers 已经被设置，不能重复设置。"),
        };
    }
}
