using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
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
    
    public SchemaContractConvertedRect Rect { get; init; }

    public Dictionary<string, int>? Scores { get; init; }
}

public sealed record SchemaContractOutput
{
    public required string? RequiredNullableName { get; init; }

    [JsonIgnore]
    public string? IgnoredValue { get; init; }

    public SchemaContractEnum Mode { get; init; }
    
    public SchemaContractConvertedRect Rect { get; init; }
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

[JsonConverter(typeof(MultipleDoubleJsonConverter<SchemaContractConvertedRect>))]
public readonly record struct SchemaContractConvertedRect : IParsable<SchemaContractConvertedRect>
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }

    public static SchemaContractConvertedRect Parse(string s, IFormatProvider? provider)
    {
        Span<double> parts = stackalloc double[4];
        MultipleDoubleJsonConverter<SchemaContractConvertedRect>.TryParseToValues(s, parts, provider);
        return new SchemaContractConvertedRect
        {
            X = parts[0],
            Y = parts[1],
            Width = parts[2],
            Height = parts[3],
        };
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SchemaContractConvertedRect result)
    {
        Span<double> parts = stackalloc double[4];
        var success = MultipleDoubleJsonConverter<SchemaContractConvertedRect>.TryParseToValues(s, parts, provider);
        result = new SchemaContractConvertedRect
        {
            X = parts[0],
            Y = parts[1],
            Width = parts[2],
            Height = parts[3],
        };
        return success;
    }

    public override string ToString()
    {
        return $"{X},{Y},{Width},{Height}";
    }
}

internal sealed class MultipleDoubleJsonConverter<T> : JsonConverter<T> where T : IParsable<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string in format 'a,b,c,d' but got {reader.TokenType}.");
        }

        return T.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }

    internal static bool TryParseToValues(string? text, Span<double> values, IFormatProvider? provider)
    {
        if (text is null)
        {
            return false;
        }
        var parts = text.Split(',');
        if (parts.Length != values.Length)
        {
            throw new JsonException($"Expected {values.Length} comma-separated values (a,b,c,d) but got {parts.Length}.");
        }

        var success = true;
        for (var i = 0; i < values.Length; i++)
        {
            success = success && double.TryParse(parts[i], NumberStyles.Float, provider, out values[i]);
        }
        return success;
    }
}
