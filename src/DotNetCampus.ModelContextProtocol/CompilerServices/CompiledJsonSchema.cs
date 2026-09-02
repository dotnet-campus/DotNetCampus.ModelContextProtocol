using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.Exceptions;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 表示 MCP 工具的输入输出的 JSON Schema，由源生成器生成。
/// </summary>
public sealed record CompiledJsonSchema
{
    /// <summary>
    /// 提供运行时属性类型，以便能将编译时 Schema 映射到包含 System.Text.Json 元数据信息的 Json 属性信息。
    /// </summary>
    [JsonIgnore]
    public Type? RuntimeType { get; init; }

    /// <summary>
    /// 提供运行时属性名，以便能将编译时 Schema 映射到包含 System.Text.Json 元数据信息的 Json 属性。
    /// </summary>
    [JsonIgnore]
    public string? RuntimePropertyName { get; init; }

    /// <summary>
    /// Schema 类型（可能是字符串或数组，数组用于表示可空类型）。<br/>
    /// 仅类型鉴别器此字段是 <see langword="null"/>，且需显式赋值。
    /// </summary>
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Type { get; init; }

    /// <summary>
    /// 枚举值列表（仅用于枚举类型）。
    /// </summary>
    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Enum { get; init; }

    /// <summary>
    /// 常量值（用于约束属性必须等于特定值，如多态鉴别器）。
    /// </summary>
    [JsonPropertyName("const")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Const { get; init; }

    /// <summary>
    /// 枚举值的显示名称列表（与 Enum 对应，仅用于枚举类型）。
    /// </summary>
    [JsonPropertyName("enumNames")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? EnumNames { get; init; }

    /// <summary>
    /// 默认值（未设置默认值时需设为 <see langword="null"/>，
    /// 显式设置了默认值时必须设置值，即使值的含义为 null）。
    /// </summary>
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Default { get; init; }

    /// <summary>
    /// 参数描述
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// 对象属性定义（仅用于 object 类型）。
    /// </summary>
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, CompiledJsonSchema>? Properties { get; init; }

    /// <summary>
    /// 必需属性列表（仅用于 object 类型）。
    /// </summary>
    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Required { get; init; }

    /// <summary>
    /// 数组项类型定义（仅用于 array 类型）。
    /// </summary>
    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompiledJsonSchema? Items { get; init; }

    /// <summary>
    /// 多态类型的可能子类型列表。
    /// </summary>
    [JsonPropertyName("anyOf")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CompiledJsonSchema>? AnyOf { get; init; }

    /// <summary>
    /// 指示对象类型是否允许额外属性（未在 properties 中定义的属性）。<br/>
    /// 当设置为 true 时，允许任意额外属性；当设置为 false 时，不允许额外属性；<br/>
    /// 当设置为 Schema 对象时，额外属性必须符合该 Schema。
    /// </summary>
    [JsonPropertyName("additionalProperties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? AdditionalProperties { get; init; }

    /// <summary>
    /// 将编译期生成的 Schema 模板按指定 JSON 序列化上下文转换为最终可输出的 JSON Schema。
    /// </summary>
    /// <param name="jsonSerializerContext">业务类型的 JSON 序列化上下文。</param>
    /// <returns>最终可输出的 JSON Schema。</returns>
    public JsonElement ToJsonElement(JsonSerializerContext jsonSerializerContext)
    {
        var schema = ApplyJsonContract(jsonSerializerContext.GetTypeInfo, jsonSerializerContext.GetType().FullName);

        return JsonSerializer.SerializeToElement(schema, CompiledSchemaJsonContext.Default.CompiledJsonSchema);
    }

    /// <summary>
    /// 将编译期生成的 Schema 模板按指定 JSON 类型信息转换为最终可输出的 JSON Schema。
    /// </summary>
    /// <param name="schemaJsonContext">用于序列化 Schema 对象的 JSON 上下文。</param>
    /// <param name="jsonTypeInfo">业务类型的 JSON 类型信息。</param>
    /// <returns>最终可输出的 JSON Schema。</returns>
    public JsonElement ToJsonElement<T>(JsonSerializerContext schemaJsonContext, JsonTypeInfo<T> jsonTypeInfo)
    {
        var schema = ApplyJsonContract(runtimeType => GetJsonTypeInfo(jsonTypeInfo, runtimeType), jsonTypeInfo.Options.TypeInfoResolver?.GetType().FullName);

        return JsonSerializer.SerializeToElement(schema, GetSchemaJsonTypeInfo(schemaJsonContext));
    }

    private static JsonTypeInfo<CompiledJsonSchema> GetSchemaJsonTypeInfo(JsonSerializerContext schemaJsonContext)
    {
        if (schemaJsonContext.GetTypeInfo(typeof(CompiledJsonSchema)) is JsonTypeInfo<CompiledJsonSchema> schemaJsonTypeInfo)
        {
            return schemaJsonTypeInfo;
        }

        throw new InvalidOperationException($"{schemaJsonContext.GetType().FullName} does not contain JsonTypeInfo for {typeof(CompiledJsonSchema).FullName}.");
    }

    private CompiledJsonSchema ApplyJsonContract(Func<Type, JsonTypeInfo?> getJsonTypeInfo, string? jsonSerializerContextTypeName)
    {
        var properties = Properties?.ToDictionary(
            x => x.Key,
            x => x.Value.ApplyJsonContract(getJsonTypeInfo, jsonSerializerContextTypeName),
            StringComparer.Ordinal);

        var schema = this with
        {
            Properties = properties,
            Items = Items?.ApplyJsonContract(getJsonTypeInfo, jsonSerializerContextTypeName),
            AnyOf = AnyOf?.Select(x => x.ApplyJsonContract(getJsonTypeInfo, jsonSerializerContextTypeName)).ToList(),
        };

        if (RuntimeType is null)
        {
            return schema;
        }

        var jsonTypeInfo = getJsonTypeInfo(RuntimeType);
        if (jsonTypeInfo is null)
        {
            throw CreateJsonTypeInfoNotFoundException(RuntimeType, jsonSerializerContextTypeName);
        }

        if (properties is null || jsonTypeInfo.Kind is not JsonTypeInfoKind.Object)
        {
            return schema;
        }

        var rewrittenProperties = new Dictionary<string, CompiledJsonSchema>(StringComparer.Ordinal);
        var rewrittenRequired = new List<string>();
        var requiredSourceNames = Required is null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(Required, StringComparer.Ordinal);

        foreach (var jsonPropertyInfo in jsonTypeInfo.Properties)
        {
            var runtimePropertyName = GetRuntimePropertyName(jsonPropertyInfo);
            var generatedProperty = properties.Values.FirstOrDefault(x =>
                string.Equals(x.RuntimePropertyName, runtimePropertyName, StringComparison.Ordinal) ||
                string.Equals(x.RuntimePropertyName, jsonPropertyInfo.Name, StringComparison.Ordinal) ||
                properties.ContainsKey(jsonPropertyInfo.Name) && ReferenceEquals(properties[jsonPropertyInfo.Name], x));

            if (generatedProperty is null)
            {
                continue;
            }

            rewrittenProperties[jsonPropertyInfo.Name] = generatedProperty;
            if (jsonPropertyInfo.IsRequired || requiredSourceNames.Contains(generatedProperty.RuntimePropertyName ?? jsonPropertyInfo.Name))
            {
                rewrittenRequired.Add(jsonPropertyInfo.Name);
            }
        }

        return schema with
        {
            Properties = rewrittenProperties,
            Required = rewrittenRequired.Count == 0 ? null : rewrittenRequired,
        };
    }

    private static JsonTypeInfo? GetJsonTypeInfo(JsonTypeInfo rootJsonTypeInfo, Type runtimeType)
    {
        if (rootJsonTypeInfo.Type == runtimeType)
        {
            return rootJsonTypeInfo;
        }

        try
        {
            return rootJsonTypeInfo.Options.GetTypeInfo(runtimeType);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static McpToolJsonTypeInfoNotFoundException CreateJsonTypeInfoNotFoundException(Type runtimeType, string? jsonSerializerContextTypeName)
    {
        var typeName = runtimeType.Name;
        var typeFullName = runtimeType.FullName ?? runtimeType.ToString();
        return jsonSerializerContextTypeName is null
            ? new McpToolJsonTypeInfoNotFoundException(typeName, typeFullName)
            : new McpToolJsonTypeInfoNotFoundException(typeName, typeFullName, jsonSerializerContextTypeName);
    }

    private static string? GetRuntimePropertyName(JsonPropertyInfo propertyInfo)
    {
        return propertyInfo.AttributeProvider switch
        {
            MemberInfo memberInfo => memberInfo.Name,
            ParameterInfo parameterInfo => parameterInfo.Name,
            _ => null,
        };
    }
}
