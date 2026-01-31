using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Servers;

public interface IMcpRequestTracer
{
    void CallTool(McpCallToolTraceInfo info);
}

internal sealed class EmptyMcpRequestTracer : IMcpRequestTracer
{
}

public readonly record struct McpCallToolTraceInfo
{
    public required string? ToolName { get; init; }
    public required JsonElement? InputArguments { get; init; }
    public required JsonElement? RawRequest { get; init; }
    public required JsonElement? Result { get; init; }
}
