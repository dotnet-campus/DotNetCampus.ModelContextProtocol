using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Tests.McpTools;

public sealed class SchemaContractTool
{
    [McpServerTool(ReadOnly = true)]
    public string EchoEnum(
        SchemaContractEnum value = SchemaContractEnum.JsonObject,
        IReadOnlyList<SchemaContractEnum>? values = null)
    {
        return $"{value}:{values?.Count ?? 0}";
    }

    [McpServerTool(ReadOnly = true)]
    public SchemaContractEnum ReturnEnum()
    {
        return SchemaContractEnum.JsonObject;
    }

    [McpServerTool(ReadOnly = true)]
    public IReadOnlyList<SchemaContractEnum> ReturnEnumList()
    {
        return [SchemaContractEnum.PlainText, SchemaContractEnum.JsonObject];
    }

    [McpServerTool(ReadOnly = true)]
    public string EchoContractObject(SchemaContractInput input)
    {
        return input.RequiredNullableName ?? string.Empty;
    }

    [McpServerTool(ReadOnly = true)]
    public string EchoContractInputObject(
        [ToolParameter(Type = ToolParameterType.InputObject)]
        SchemaContractInput input)
    {
        return input.RequiredNullableName ?? string.Empty;
    }

    [McpServerTool(ReadOnly = true)]
    public string EchoDictionary(Dictionary<string, int> values)
    {
        return values.Count.ToString();
    }

    [McpServerTool(ReadOnly = true)]
    public string EchoPolymorphic(SchemaContractPolymorphicBase value)
    {
        return value.GetType().Name;
    }

    [McpServerTool(ReadOnly = true)]
    public SchemaContractOutput ReturnContractOutput()
    {
        return new SchemaContractOutput
        {
            RequiredNullableName = null,
            Mode = SchemaContractEnum.JsonObject,
        };
    }
}

public enum SchemaContractEnum
{
    PlainText = 0,

    [JsonStringEnumMemberName("jsonObject")]
    JsonObject = 10,
}

public sealed record SchemaContractInput
{
    public required string? RequiredNullableName { get; init; }

    [JsonRequired]
    public string? JsonRequiredValue { get; init; }

    [JsonIgnore]
    public string? IgnoredValue { get; init; }

    [JsonPropertyName("custom_mode")]
    public SchemaContractEnum Mode { get; init; }

    public Dictionary<string, int>? Scores { get; init; }
}

public sealed record SchemaContractOutput
{
    public required string? RequiredNullableName { get; init; }

    [JsonIgnore]
    public string? IgnoredValue { get; init; }

    public SchemaContractEnum Mode { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SchemaContractPolymorphicA), 1)]
[JsonDerivedType(typeof(SchemaContractPolymorphicB), "b")]
public abstract record SchemaContractPolymorphicBase;

public sealed record SchemaContractPolymorphicA : SchemaContractPolymorphicBase
{
    public required string Name { get; init; }
}

public sealed record SchemaContractPolymorphicB : SchemaContractPolymorphicBase
{
    public required int Count { get; init; }
}
