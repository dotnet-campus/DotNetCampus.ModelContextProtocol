#nullable enable
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol;
using global::System.Text.Json;

namespace DotNetCampus.SampleMcpServer.McpTools;

/// <summary>
/// 为 <see cref="global::DotNetCampus.SampleMcpServer.McpTools.SampleTools.Delay"/> 方法生成的 MCP 服务器工具桥接类。
/// </summary>
public sealed class SampleTools_Delay_Bridge2(global::System.Func<global::DotNetCampus.SampleMcpServer.McpTools.SampleTools> targetFactory) : global::DotNetCampus.ModelContextProtocol.Servers.IMcpServerTool
{
    private readonly global::System.Func<global::DotNetCampus.SampleMcpServer.McpTools.SampleTools> _targetFactory = targetFactory;

    private global::DotNetCampus.SampleMcpServer.McpTools.SampleTools Target => _targetFactory();

    /// <inheritdoc />
    public string ToolName { get; } = "delay";

    /// <inheritdoc />
    public global::DotNetCampus.ModelContextProtocol.Protocol.Tool GetToolDefinition(InputSchemaJsonObjectJsonContext jsonContext) => new()
    {
        Name = "delay",
        Title = null,
        Description = "描述信息未提供",
        InputSchema = JsonSerializer.SerializeToElement(GetInputSchema(jsonContext), jsonContext.InputSchemaJsonObject),
    };

    private InputSchemaJsonObject GetInputSchema(InputSchemaJsonObjectJsonContext jsonContext) => new()
    {
        RawType = JsonSerializer.SerializeToElement("string", jsonContext.IReadOnlyListString),
        Default = "",
        Enum = null,
        Description = "",
        Items = null,
        Properties = null,
        Required = null,
    };

    /// <inheritdoc />
    public async global::System.Threading.Tasks.ValueTask<global::DotNetCampus.ModelContextProtocol.Protocol.CallToolResult> CallTool(
        global::System.Text.Json.JsonElement jsonArguments,
        global::System.Text.Json.Serialization.JsonSerializerContext jsonSerializerContext,
        global::System.Threading.CancellationToken cancellationToken)
    {
        var minutes = jsonArguments.TryGetProperty("minutes", out var minutesProperty)
            ? minutesProperty.Deserialize(
                (global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<int>)jsonSerializerContext.GetTypeInfo(typeof(int))!)
            : 0;
        var seconds = jsonArguments.TryGetProperty("seconds", out var secondsProperty)
            ? secondsProperty.Deserialize(
                (global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<int>)jsonSerializerContext.GetTypeInfo(typeof(int))!)
            : 0;
        var milliseconds = jsonArguments.TryGetProperty("milliseconds", out var millisecondsProperty)
            ? millisecondsProperty.Deserialize(
                (global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<int>)jsonSerializerContext.GetTypeInfo(typeof(int))!)
            : 0;
        var result = await Target.Delay(minutes, seconds, milliseconds, cancellationToken).ConfigureAwait(false);
        return (global::DotNetCampus.ModelContextProtocol.Protocol.CallToolResult)result;
    }
}
