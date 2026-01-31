using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 从服务器到客户端的请求，通过客户端从用户引出额外信息。<br/>
/// A request from the server to elicit additional information from the user via the client.
/// </summary>
public sealed record ElicitRequestParams : RequestParams
{
    /// <summary>
    /// 要呈现给用户的消息。<br/>
    /// The message to present to the user.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// 可选的引出模式，可以是 "form" 或 "url"。<br/>
    /// Form 模式：可省略（默认 "form"），url 和 elicitationId 为 null。<br/>
    /// URL 模式：mode 必须为 "url"，url 和 elicitationId 必填。<br/>
    /// 验证逻辑将通过分析器实现。<br/>
    /// Optional elicitation mode, can be "form" or "url".<br/>
    /// Form mode: mode can be omitted (defaults to "form"), url and elicitationId are null.<br/>
    /// URL mode: mode must be "url", url and elicitationId are required.<br/>
    /// Validation logic will be implemented via analyzer.
    /// </summary>
    [JsonPropertyName("mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; init; }

    /// <summary>
    /// JSON Schema 的受限子集。<br/>
    /// 仅允许顶级属性，不允许嵌套。<br/>
    /// 在 form 模式下必填，在 URL 模式下可选。<br/>
    /// A restricted subset of JSON Schema.<br/>
    /// Only top-level properties are allowed, without nesting.<br/>
    /// Required in form mode, optional in URL mode.
    /// </summary>
    [JsonPropertyName("requestedSchema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ElicitationSchema? RequestedSchema { get; init; }

    /// <summary>
    /// URL 模式下的目标 URL。<br/>
    /// 在 URL 模式下必填，在 form 模式下为 null。<br/>
    /// The target URL in URL mode.<br/>
    /// Required in URL mode, null in form mode.
    /// </summary>
    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; init; }

    /// <summary>
    /// URL 模式下的引出标识符。<br/>
    /// 在 URL 模式下必填，在 form 模式下为 null。<br/>
    /// The elicitation identifier in URL mode.<br/>
    /// Required in URL mode, null in form mode.
    /// </summary>
    [JsonPropertyName("elicitationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ElicitationId { get; init; }
}

/// <summary>
/// 引出请求的 Schema 定义<br/>
/// Schema definition for elicitation request
/// </summary>
public sealed record ElicitationSchema
{
    /// <summary>
    /// 类型必须为 "object"<br/>
    /// Type must be "object"
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; } = "object";

    /// <summary>
    /// 属性定义<br/>
    /// Property definitions
    /// </summary>
    [JsonPropertyName("properties")]
    public required Dictionary<string, PrimitiveSchemaDefinition> Properties { get; init; }

    /// <summary>
    /// 必需的属性名称列表<br/>
    /// List of required property names
    /// </summary>
    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Required { get; init; }
}

/// <summary>
/// 限制的 schema 定义，仅允许基本类型，不允许嵌套对象或数组。<br/>
/// Restricted schema definitions that only allow primitive types without nested objects or arrays.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StringSchema), typeDiscriminator: "string")]
[JsonDerivedType(typeof(NumberSchema), typeDiscriminator: "number")]
[JsonDerivedType(typeof(BooleanSchema), typeDiscriminator: "boolean")]
[JsonDerivedType(typeof(SingleSelectEnumSchema), typeDiscriminator: "string")]
[JsonDerivedType(typeof(MultiSelectEnumSchema), typeDiscriminator: "array")]
public abstract record PrimitiveSchemaDefinition
{
    /// <summary>
    /// 可选的标题<br/>
    /// Optional title
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// 可选的描述<br/>
    /// Optional description
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}

/// <summary>
/// 字符串类型的 Schema 定义<br/>
/// String type schema definition
/// </summary>
public sealed record StringSchema : PrimitiveSchemaDefinition
{
    /// <summary>
    /// 类型<br/>
    /// Type
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "string";

    /// <summary>
    /// 最小长度<br/>
    /// Minimum length
    /// </summary>
    [JsonPropertyName("minLength")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MinLength { get; init; }

    /// <summary>
    /// 最大长度<br/>
    /// Maximum length
    /// </summary>
    [JsonPropertyName("maxLength")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxLength { get; init; }

    /// <summary>
    /// 格式（例如 "email", "uri", "date-time"）<br/>
    /// Format (e.g., "email", "uri", "date-time")
    /// </summary>
    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; init; }

    /// <summary>
    /// 正则表达式模式<br/>
    /// Regular expression pattern
    /// </summary>
    [JsonPropertyName("pattern")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Pattern { get; init; }

    /// <summary>
    /// 默认值<br/>
    /// Default value
    /// </summary>
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Default { get; init; }
}

/// <summary>
/// 数字类型的 Schema 定义<br/>
/// Number type schema definition
/// </summary>
public sealed record NumberSchema : PrimitiveSchemaDefinition
{
    /// <summary>
    /// 类型<br/>
    /// Type
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "number";

    /// <summary>
    /// 最小值<br/>
    /// Minimum value
    /// </summary>
    [JsonPropertyName("minimum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Minimum { get; init; }

    /// <summary>
    /// 最大值<br/>
    /// Maximum value
    /// </summary>
    [JsonPropertyName("maximum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Maximum { get; init; }

    /// <summary>
    /// 倍数<br/>
    /// Multiple of
    /// </summary>
    [JsonPropertyName("multipleOf")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MultipleOf { get; init; }

    /// <summary>
    /// 默认值<br/>
    /// Default value
    /// </summary>
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Default { get; init; }
}

/// <summary>
/// 布尔类型的 Schema 定义<br/>
/// Boolean type schema definition
/// </summary>
public sealed record BooleanSchema : PrimitiveSchemaDefinition
{
    /// <summary>
    /// 类型<br/>
    /// Type
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "boolean";

    /// <summary>
    /// 默认值<br/>
    /// Default value
    /// </summary>
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Default { get; init; }
}

/// <summary>
/// 枚举选项，用于有标题的枚举。<br/>
/// Enum option for titled enumerations.
/// </summary>
public sealed record EnumOption
{
    /// <summary>
    /// 枚举值。<br/>
    /// The enum value.
    /// </summary>
    [JsonPropertyName("const")]
    public required string Const { get; init; }

    /// <summary>
    /// 显示标题。<br/>
    /// Display title.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }
}

/// <summary>
/// 单选枚举 Schema 定义。<br/>
/// 支持无标题枚举（使用 enum 字段）和有标题枚举（使用 oneOf 字段）。<br/>
/// enumNames 为遗留字段，将在未来版本中移除。<br/>
/// Single-select enumeration schema definition.<br/>
/// Supports untitled enumerations (using enum field) and titled enumerations (using oneOf field).<br/>
/// enumNames is a legacy field and will be removed in future versions.
/// </summary>
public sealed record SingleSelectEnumSchema : PrimitiveSchemaDefinition
{
    /// <summary>
    /// 类型，必须为 "string"。<br/>
    /// Type, must be "string".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "string";

    /// <summary>
    /// 无标题枚举：枚举值数组。<br/>
    /// Untitled enum: array of enum values.
    /// </summary>
    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Enum { get; init; }

    /// <summary>
    /// 有标题枚举：使用 oneOf + const + title 的枚举选项数组。<br/>
    /// Titled enum: array of enum options using oneOf + const + title.
    /// </summary>
    [JsonPropertyName("oneOf")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<EnumOption>? OneOf { get; init; }

    /// <summary>
    /// 遗留字段：枚举值的显示名称（非 JSON Schema 2020-12 标准）。<br/>
    /// 此字段将在未来版本中移除。<br/>
    /// Legacy field: display names for enum values (non-standard according to JSON Schema 2020-12).<br/>
    /// This field will be removed in future versions.
    /// </summary>
    [JsonPropertyName("enumNames")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? EnumNames { get; init; }

    /// <summary>
    /// 默认值，必须是 enum 中的某个值。<br/>
    /// Default value, must be one of the enum values.
    /// </summary>
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Default { get; init; }
}

/// <summary>
/// 多选枚举的项目 Schema。<br/>
/// Schema for multi-select enum items.
/// </summary>
public sealed record MultiSelectEnumItemsSchema
{
    /// <summary>
    /// 项目类型，必须为 "string"。<br/>
    /// Item type, must be "string".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "string";

    /// <summary>
    /// 无标题枚举：枚举值数组。<br/>
    /// Untitled enum: array of enum values.
    /// </summary>
    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Enum { get; init; }

    /// <summary>
    /// 有标题枚举：使用 anyOf + const + title 的枚举选项数组。<br/>
    /// Titled enum: array of enum options using anyOf + const + title.
    /// </summary>
    [JsonPropertyName("anyOf")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<EnumOption>? AnyOf { get; init; }
}

/// <summary>
/// 多选枚举 Schema 定义。<br/>
/// 支持无标题枚举（items.enum）和有标题枚举（items.anyOf）。<br/>
/// Multi-select enumeration schema definition.<br/>
/// Supports untitled enumerations (items.enum) and titled enumerations (items.anyOf).
/// </summary>
public sealed record MultiSelectEnumSchema : PrimitiveSchemaDefinition
{
    /// <summary>
    /// 类型，必须为 "array"。<br/>
    /// Type, must be "array".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "array";

    /// <summary>
    /// 最少选择数量。<br/>
    /// Minimum number of items to select.
    /// </summary>
    [JsonPropertyName("minItems")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MinItems { get; init; }

    /// <summary>
    /// 最多选择数量。<br/>
    /// Maximum number of items to select.
    /// </summary>
    [JsonPropertyName("maxItems")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxItems { get; init; }

    /// <summary>
    /// 数组项目的 Schema 定义。<br/>
    /// Schema definition for array items.
    /// </summary>
    [JsonPropertyName("items")]
    public required MultiSelectEnumItemsSchema Items { get; init; }

    /// <summary>
    /// 默认值，必须是 enum 中的值数组。<br/>
    /// Default value, must be an array of enum values.
    /// </summary>
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Default { get; init; }
}

/// <summary>
/// 客户端对服务器的 elicitation/create 请求的响应。<br/>
/// The client's response to an elicitation/create request from the server.
/// </summary>
public sealed record ElicitResult : Result
{
    /// <summary>
    /// 从用户收集的数据，符合请求的 schema。<br/>
    /// The data collected from the user, conforming to the requested schema.
    /// </summary>
    [JsonPropertyName("data")]
    public required Dictionary<string, object> Data { get; init; }
}

/// <summary>
/// 引出完成通知。<br/>
/// Elicitation completion notification.
/// </summary>
public sealed record ElicitationCompleteNotification : JsonRpcNotification
{
    /// <summary>
    /// 通知参数。<br/>
    /// Notification parameters.
    /// </summary>
    [JsonPropertyName("params")]
    public new required ElicitationCompleteNotificationParams Params { get; init; }
}

/// <summary>
/// 引出完成通知的参数。<br/>
/// Parameters for elicitation completion notification.
/// </summary>
public sealed record ElicitationCompleteNotificationParams
{
    /// <summary>
    /// 引出标识符。<br/>
    /// The elicitation identifier.
    /// </summary>
    [JsonPropertyName("elicitationId")]
    public required string ElicitationId { get; init; }

    /// <summary>
    /// 元数据字段<br/>
    /// See <a href="https://modelcontextprotocol.io/specification/2025-11-25/basic/index#meta">
    /// General fields: _meta</a> for notes on _meta usage.
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Meta { get; init; }
}
