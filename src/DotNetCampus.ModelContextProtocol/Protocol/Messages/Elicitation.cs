using System.Text.Json.Serialization;

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
    /// JSON Schema 的受限子集。<br/>
    /// 仅允许顶级属性，不允许嵌套。<br/>
    /// A restricted subset of JSON Schema.<br/>
    /// Only top-level properties are allowed, without nesting.
    /// </summary>
    [JsonPropertyName("requestedSchema")]
    public required ElicitationSchema RequestedSchema { get; init; }
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
[JsonDerivedType(typeof(EnumSchema), typeDiscriminator: "enum")]
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
}

/// <summary>
/// 枚举类型的 Schema 定义<br/>
/// Enum type schema definition
/// </summary>
public sealed record EnumSchema : PrimitiveSchemaDefinition
{
    /// <summary>
    /// 类型<br/>
    /// Type
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "enum";

    /// <summary>
    /// 允许的枚举值<br/>
    /// Allowed enum values
    /// </summary>
    [JsonPropertyName("enum")]
    public required string[] Enum { get; init; }
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
