using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Schema;

/// <summary>
/// 表示 MCP 工具的 inputSchema（JSON Schema 格式）。<br/>
/// Represents the inputSchema for an MCP tool (in JSON Schema format).
/// </summary>
public sealed record ToolInputSchema
{
    /// <summary>
    /// Schema 类型（可能是字符串或数组，数组用于表示可空类型）。<br/>
    /// 仅类型鉴别器此字段是 <see langword="null"/>，且需显式赋值。<br/>
    /// Schema type (may be a string or array, array is used to represent nullable types).<br/>
    /// Only for type discriminators, this field is <see langword="null"/> and must be explicitly assigned.
    /// </summary>
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Type { get; init; }

    /// <summary>
    /// 枚举值列表（仅用于枚举类型）。<br/>
    /// List of enumeration values (only for enum types).
    /// </summary>
    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Enum { get; init; }

    /// <summary>
    /// 常量值（用于约束属性必须等于特定值，如多态鉴别器）。<br/>
    /// Constant value (used to constrain a property to a specific value, such as a polymorphic discriminator).
    /// </summary>
    [JsonPropertyName("const")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Const { get; init; }

    /// <summary>
    /// 枚举值的显示名称列表（与 Enum 对应，仅用于枚举类型）。<br/>
    /// List of display names for enumeration values (corresponding to Enum, only for enum types).
    /// </summary>
    [JsonPropertyName("enumNames")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? EnumNames { get; init; }

    /// <summary>
    /// 默认值（未设置默认值时需设为 <see langword="null"/>，
    /// 显式设置了默认值时必须设置值，即使值的含义为 null）。<br/>
    /// Default value (should be set to <see langword="null"/> when no default value is set,
    /// must be set when a default value is explicitly set, even if the value means null).
    /// </summary>
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Default { get; init; }

    /// <summary>
    /// 参数描述<br/>
    /// Parameter description
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// 对象属性定义（仅用于 object 类型）。<br/>
    /// Object property definitions (only for object type).
    /// </summary>
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, ToolInputSchema>? Properties { get; init; }

    /// <summary>
    /// 必需属性列表（仅用于 object 类型）。<br/>
    /// List of required properties (only for object type).
    /// </summary>
    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Required { get; init; }

    /// <summary>
    /// 数组项类型定义（仅用于 array 类型）。<br/>
    /// Array item type definition (only for array type).
    /// </summary>
    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolInputSchema? Items { get; init; }

    /// <summary>
    /// 多态类型的可能子类型列表。<br/>
    /// List of possible subtypes for polymorphic types.
    /// </summary>
    [JsonPropertyName("anyOf")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ToolInputSchema>? AnyOf { get; init; }
}
