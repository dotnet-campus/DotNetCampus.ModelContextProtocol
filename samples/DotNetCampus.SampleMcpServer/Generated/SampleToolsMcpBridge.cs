using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.SampleMcpServer.McpTools;

namespace DotNetCampus.SampleMcpServer.Generated;

public class McpServerToolBridge_SampleTools_EchoInfo : IGeneratedMcpServerToolBridge
{
    private readonly Func<SampleTools> _targetGetter;

    public string ToolName { get; } = "echo-info";

    private SampleTools Target => _targetGetter();

    /// <inheritdoc />
    public Tool GetToolDefinition() => new()
    {
        Title = "",
    };

    public ValueTask<CallToolResult> CallTool(JsonElement jsonArguments, JsonSerializerContext jsonSerializerContext)
    {
        var text = jsonArguments.TryGetProperty("text", out var textProperty)
            ? textProperty.Deserialize(
                (JsonTypeInfo<string>)jsonSerializerContext.GetTypeInfo(typeof(string))!)
            : throw new MissingRequiredArgumentException("text");
        var options = jsonArguments.TryGetProperty("options", out var optionsProperty)
            ? optionsProperty.Deserialize(
                (JsonTypeInfo<EchoOptions>)jsonSerializerContext.GetTypeInfo(typeof(EchoOptions))!)
            : throw new MissingRequiredArgumentException("options");
        var count = jsonArguments.TryGetProperty("count", out var countProperty)
            ? countProperty.Deserialize(
                (JsonTypeInfo<int>)jsonSerializerContext.GetTypeInfo(typeof(int))!)
            : 1;
        var extraData = jsonArguments.TryGetProperty("extraData", out var extraDataProperty)
            ? extraDataProperty.Deserialize(
                (JsonTypeInfo<EchoExtraData?>)jsonSerializerContext.GetTypeInfo(typeof(EchoExtraData))!)
            : null;
        var isError = jsonArguments.TryGetProperty("isError", out var isErrorProperty)
            ? isErrorProperty.Deserialize(
                (JsonTypeInfo<bool>)jsonSerializerContext.GetTypeInfo(typeof(bool))!)
            : false;
        var result = Target.EchoInfo(
            text!,
            options,
            count,
            extraData,
            isError);
        return ValueTask.FromResult(result);
    }
}
