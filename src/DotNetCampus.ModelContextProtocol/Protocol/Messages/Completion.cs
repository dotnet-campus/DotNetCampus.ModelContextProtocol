using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 从客户端到服务器的请求，以请求完成选项。<br/>
/// A request from the client to the server, to ask for completion options.
/// </summary>
public sealed record CompleteRequestParams : RequestParams
{
    /// <summary>
    /// 引用（提示词或资源模板）<br/>
    /// Reference (prompt or resource template)
    /// </summary>
    [JsonPropertyName("ref")]
    public required CompletionReference Ref { get; init; }

    /// <summary>
    /// 参数信息<br/>
    /// Argument information
    /// </summary>
    [JsonPropertyName("argument")]
    public required CompletionArgument Argument { get; init; }

    /// <summary>
    /// 用于完成的额外可选上下文<br/>
    /// Additional, optional context for completions
    /// </summary>
    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompletionContext? Context { get; init; }
}

/// <summary>
/// 完成参数<br/>
/// Completion argument
/// </summary>
public sealed record CompletionArgument
{
    /// <summary>
    /// 参数的名称<br/>
    /// The name of the argument
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 用于完成匹配的参数值。<br/>
    /// The value of the argument to use for completion matching.
    /// </summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }
}

/// <summary>
/// 完成上下文<br/>
/// Completion context
/// </summary>
public sealed record CompletionContext
{
    /// <summary>
    /// URI 模板或提示词中先前解析的变量。<br/>
    /// Previously-resolved variables in a URI template or prompt.
    /// </summary>
    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Arguments { get; init; }
}

/// <summary>
/// 完成引用基类<br/>
/// Base class for completion references
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PromptReference), typeDiscriminator: "ref/prompt")]
[JsonDerivedType(typeof(ResourceTemplateReference), typeDiscriminator: "ref/resource")]
public abstract record CompletionReference
{
}

/// <summary>
/// 标识提示词的引用。<br/>
/// Identifies a prompt.
/// </summary>
public sealed record PromptReference : CompletionReference, IBaseMetadata
{
    /// <summary>
    /// 用于编程或逻辑使用，但在过去的规范中或作为后备用于显示名称（如果 title 不存在）。<br/>
    /// Intended for programmatic or logical use, but used as a display name in past specs
    /// or fallback (if title isn't present).
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 用于 UI 和最终用户上下文 — 优化为可读并易于理解，即使对不熟悉特定领域术语的人也是如此。<br/>
    /// 如果未提供，应使用 name 作为显示名称。<br/>
    /// Intended for UI and end-user contexts — optimized to be human-readable
    /// and easily understood, even by those unfamiliar with domain-specific terminology.<br/>
    /// If not provided, the name should be used for display.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }
}

/// <summary>
/// 对资源或资源模板定义的引用。<br/>
/// A reference to a resource or resource template definition.
/// </summary>
public sealed record ResourceTemplateReference : CompletionReference
{
    /// <summary>
    /// 资源的 URI 或 URI 模板。<br/>
    /// The URI or URI template of the resource.
    /// </summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }
}

/// <summary>
/// 服务器对客户端的 completion/complete 请求的响应。<br/>
/// The server's response to a completion/complete request from the client.
/// </summary>
public sealed record CompleteResult : Result
{
    /// <summary>
    /// 完成选项<br/>
    /// Completion options
    /// </summary>
    [JsonPropertyName("completion")]
    public required CompletionValue Completion { get; init; }
}

/// <summary>
/// 完成值<br/>
/// Completion value
/// </summary>
public sealed record CompletionValue
{
    /// <summary>
    /// 一组可能的补全值。<br/>
    /// 值的顺序是服务器建议的显示顺序，最相关的补全排在前面。<br/>
    /// An array of completion values. Values earlier in the array are interpreted
    /// as higher priority, representing the server's suggested ordering.
    /// </summary>
    [JsonPropertyName("values")]
    public required IReadOnlyList<string> Values { get; init; }

    /// <summary>
    /// 可用补全的总数。<br/>
    /// 如果设置，这表示服务器知道的值的总数，即使它可能只返回其中的子集。<br/>
    /// 如果未设置或为 null，则表示总数未知。<br/>
    /// 这与响应中实际发送的值的数量不同。<br/>
    /// The total number of completion options available.<br/>
    /// If set, this indicates the total count of values the server is aware of,
    /// even if it may only be returning a subset of them.<br/>
    /// If not set or null, the total count is unknown.<br/>
    /// This may differ from the number of values actually sent in the response.
    /// </summary>
    [JsonPropertyName("total")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Total { get; init; }

    /// <summary>
    /// 指示是否存在超出当前响应中提供的其他完成选项，即使确切的总数未知。<br/>
    /// Indicates whether there are additional completion options beyond those provided
    /// in the current response, even if the exact total is unknown.
    /// </summary>
    [JsonPropertyName("hasMore")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasMore { get; init; }
}
