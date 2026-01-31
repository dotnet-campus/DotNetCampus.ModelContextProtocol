using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 服务器提供的提示词或提示词模板。<br/>
/// A prompt or prompt template that the server offers.
/// </summary>
public sealed record Prompt : IBaseMetadata
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

    /// <summary>
    /// 此提示词的用途描述。<br/>
    /// 客户端可以使用这些信息来改善 LLM 对可用提示词的理解。<br/>
    /// 可以将其视为给模型的"提示"。<br/>
    /// A description of what this prompt does.<br/>
    /// This can be used by clients to improve the LLM's understanding of available prompts.
    /// It can be thought of like a "hint" to the model.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// 此提示词接受的参数列表（如果有）。<br/>
    /// A list of arguments to use for templating the prompt, if any.
    /// </summary>
    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PromptArgument>? Arguments { get; init; }

    /// <summary>
    /// 图标列表<br/>
    /// List of icons
    /// </summary>
    [JsonPropertyName("icons")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<Icon>? Icons { get; init; }

    /// <summary>
    /// 元数据字段<br/>
    /// See <a href="https://modelcontextprotocol.io/specification/2025-11-25/basic/index#meta">
    /// General fields: _meta</a> for notes on _meta usage.
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Meta { get; init; }
}

/// <summary>
/// 描述提示词可以接受的参数。<br/>
/// Describes an argument that a prompt can accept.
/// </summary>
public sealed record PromptArgument : IBaseMetadata
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

    /// <summary>
    /// 参数的人类可读描述。<br/>
    /// A human-readable description of the argument.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// 是否必须提供此参数。<br/>
    /// Whether this argument must be provided.
    /// </summary>
    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Required { get; init; }
}

/// <summary>
/// 作为提示词的一部分返回的消息。<br/>
/// 这类似于 SamplingMessage，但还支持从 MCP 服务器嵌入资源。<br/>
/// Describes a message returned as part of a prompt.<br/>
/// This is similar to SamplingMessage, but also supports the embedding of resources
/// from the MCP server.
/// </summary>
public sealed record PromptMessage
{
    /// <summary>
    /// 消息的角色<br/>
    /// The role of the message
    /// </summary>
    [JsonPropertyName("role")]
    public required Role Role { get; init; }

    /// <summary>
    /// 内容块<br/>
    /// Content block
    /// </summary>
    [JsonPropertyName("content")]
    public required ContentBlock Content { get; init; }
}

/// <summary>
/// 从客户端发送以请求服务器拥有的提示词和提示词模板列表。<br/>
/// Sent from the client to request a list of prompts and prompt templates the server has.
/// </summary>
public sealed record ListPromptsRequestParams : PaginatedRequestParams
{
}

/// <summary>
/// 服务器对客户端的 prompts/list 请求的响应。<br/>
/// The server's response to a prompts/list request from the client.
/// </summary>
public sealed record ListPromptsResult : PaginatedResult
{
    /// <summary>
    /// 提示词列表<br/>
    /// List of prompts
    /// </summary>
    [JsonPropertyName("prompts")]
    public required IReadOnlyList<Prompt> Prompts { get; init; }
}

/// <summary>
/// 客户端用于获取服务器提供的提示词。<br/>
/// Used by the client to get a prompt provided by the server.
/// </summary>
public sealed record GetPromptRequestParams : RequestParams
{
    /// <summary>
    /// 提示词或提示词模板的名称。<br/>
    /// The name of the prompt or prompt template.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 用于模板化提示词的参数。<br/>
    /// Arguments to use for templating the prompt.
    /// </summary>
    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Arguments { get; init; }
}

/// <summary>
/// 服务器对客户端的 prompts/get 请求的响应。<br/>
/// The server's response to a prompts/get request from the client.
/// </summary>
public sealed record GetPromptResult : Result
{
    /// <summary>
    /// 可选的提示词描述。<br/>
    /// An optional description for the prompt.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// 组成提示词的消息。<br/>
    /// The messages that make up the prompt.
    /// </summary>
    [JsonPropertyName("messages")]
    public required IReadOnlyList<PromptMessage> Messages { get; init; }
}
