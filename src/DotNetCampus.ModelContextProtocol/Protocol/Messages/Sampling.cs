using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 从服务器到客户端的请求，通过客户端对 LLM 进行采样。<br/>
/// 客户端对选择哪个模型拥有完全的自主权。<br/>
/// 客户端还应在开始采样之前通知用户，以允许他们检查请求（人工参与）并决定是否批准。<br/>
/// A request from the server to sample an LLM via the client.
/// The client has full discretion over which model to select.
/// The client should also inform the user before beginning sampling,
/// to allow them to inspect the request (human in the loop) and decide whether to approve it.
/// </summary>
public sealed record CreateMessageRequestParams : RequestParams
{
    /// <summary>
    /// 采样消息列表<br/>
    /// Messages for sampling
    /// </summary>
    [JsonPropertyName("messages")]
    public required IReadOnlyList<SamplingMessage> Messages { get; init; }

    /// <summary>
    /// 服务器对选择哪个模型的偏好。客户端可以忽略这些偏好。<br/>
    /// The server's preferences for which model to select. The client MAY ignore these preferences.
    /// </summary>
    [JsonPropertyName("modelPreferences")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelPreferences? ModelPreferences { get; init; }

    /// <summary>
    /// 服务器希望用于采样的可选系统提示词。客户端可以修改或省略此提示词。<br/>
    /// An optional system prompt the server wants to use for sampling.
    /// The client MAY modify or omit this prompt.
    /// </summary>
    [JsonPropertyName("systemPrompt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// 请求包含来自一个或多个 MCP 服务器（包括调用者）的上下文，以附加到提示词。<br/>
    /// 客户端可以忽略此请求。<br/>
    /// 值 "thisServer" 和 "allServers" 仅在客户端声明 ClientCapabilities.sampling.context 能力时使用，默认值为 "none"。<br/>
    /// A request to include context from one or more MCP servers (including the caller),
    /// to be attached to the prompt. The client MAY ignore this request.<br/>
    /// Values "thisServer" and "allServers" should only be used when the client declares
    /// ClientCapabilities.sampling.context capability. Default value is "none".
    /// </summary>
    [JsonPropertyName("includeContext")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IncludeContext { get; init; }

    /// <summary>
    /// 可供模型调用的工具列表。<br/>
    /// List of tools available for the model to call.
    /// </summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Tool>? Tools { get; init; }

    /// <summary>
    /// 控制模型如何选择和使用工具。<br/>
    /// Controls how the model selects and uses tools.
    /// </summary>
    [JsonPropertyName("toolChoice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolChoice? ToolChoice { get; init; }

    /// <summary>
    /// 温度参数<br/>
    /// Temperature parameter
    /// </summary>
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; init; }

    /// <summary>
    /// 请求的最大令牌数（以防止失控完成）。<br/>
    /// 客户端可以选择采样少于请求的最大值的令牌。<br/>
    /// The requested maximum number of tokens to sample (to prevent runaway completions).<br/>
    /// The client MAY choose to sample fewer tokens than the requested maximum.
    /// </summary>
    [JsonPropertyName("maxTokens")]
    public required int MaxTokens { get; init; }

    /// <summary>
    /// 停止序列<br/>
    /// Stop sequences
    /// </summary>
    [JsonPropertyName("stopSequences")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>
    /// 要传递给 LLM 提供商的可选元数据。此元数据的格式是提供商特定的。<br/>
    /// Optional metadata to pass through to the LLM provider.
    /// The format of this metadata is provider-specific.
    /// </summary>
    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Metadata { get; init; }
}

/// <summary>
/// 客户端对服务器的 sampling/create_message 请求的响应。<br/>
/// 客户端应在返回采样消息之前通知用户，以允许他们检查响应（人工参与）并决定是否允许服务器看到它。<br/>
/// The client's response to a sampling/create_message request from the server.
/// The client should inform the user before returning the sampled message, to allow them
/// to inspect the response (human in the loop) and decide whether to allow the server to see it.
/// </summary>
public sealed record CreateMessageResult : Result
{
    /// <summary>
    /// 消息角色<br/>
    /// Message role
    /// </summary>
    [JsonPropertyName("role")]
    public required Role Role { get; init; }

    /// <summary>
    /// 消息内容<br/>
    /// Message content
    /// </summary>
    [JsonPropertyName("content")]
    public required SamplingMessageContent Content { get; init; }

    /// <summary>
    /// 生成消息的模型名称。<br/>
    /// The name of the model that generated the message.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// 采样停止的原因（如果已知）。<br/>
    /// 可能的值包括："endTurn"、"stopSequence"、"maxTokens"、"toolUse"。<br/>
    /// The reason why sampling stopped, if known.<br/>
    /// Possible values include: "endTurn", "stopSequence", "maxTokens", "toolUse".
    /// </summary>
    [JsonPropertyName("stopReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StopReason { get; init; }
}

/// <summary>
/// 描述向 LLM API 发出或从 LLM API 接收的消息。<br/>
/// Describes a message issued to or received from an LLM API.
/// </summary>
public sealed record SamplingMessage
{
    /// <summary>
    /// 消息角色<br/>
    /// Message role
    /// </summary>
    [JsonPropertyName("role")]
    public required Role Role { get; init; }

    /// <summary>
    /// 消息内容<br/>
    /// Message content
    /// </summary>
    [JsonPropertyName("content")]
    public required SamplingMessageContent Content { get; init; }

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
/// 采样消息内容（文本、图像、音频、工具使用或工具结果）<br/>
/// Sampling message content (text, image, audio, tool use or tool result)
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContentBlock), typeDiscriminator: "text")]
[JsonDerivedType(typeof(ImageContentBlock), typeDiscriminator: "image")]
[JsonDerivedType(typeof(AudioContentBlock), typeDiscriminator: "audio")]
[JsonDerivedType(typeof(ToolUseContent), typeDiscriminator: "toolUse")]
[JsonDerivedType(typeof(ToolResultContent), typeDiscriminator: "toolResult")]
public abstract record SamplingMessageContent
{
}

/// <summary>
/// 服务器在采样期间对模型选择的偏好，在采样期间请求客户端。<br/>
/// 由于 LLM 可以在多个维度上变化，选择"最佳"模型很少是直截了当的。<br/>
/// 不同的模型在不同的领域表现出色 — 有些更快但功能较弱，另一些功能更强但更昂贵，等等。<br/>
/// 此接口允许服务器在多个维度上表达其优先级，以帮助客户端为其用例做出适当的选择。<br/>
/// 这些偏好始终是建议性的。客户端可以忽略它们。<br/>
/// 客户端还可以决定如何解释这些偏好以及如何在考虑其他因素的同时平衡它们。<br/>
/// The server's preferences for model selection, requested of the client during sampling.<br/>
/// Because LLMs can vary along multiple dimensions, choosing the "best" model is rarely straightforward.
/// Different models excel in different areas—some are faster but less capable, others are more capable
/// but more expensive, and so on. This interface allows servers to express their priorities across
/// multiple dimensions to help clients make an appropriate selection for their use case.<br/>
/// These preferences are always advisory. The client MAY ignore them. It is also up to the client
/// to decide how to interpret these preferences and how to balance them against other considerations.
/// </summary>
public sealed record ModelPreferences
{
    /// <summary>
    /// 用于模型选择的可选提示。<br/>
    /// 如果指定了多个提示，客户端必须按顺序评估它们（以便匹配第一个）。<br/>
    /// 客户端应优先考虑这些提示而不是数字优先级，但仍可以使用优先级从模糊匹配中进行选择。<br/>
    /// Optional hints to use for model selection.<br/>
    /// If multiple hints are specified, the client MUST evaluate them in order
    /// (such that the first match is taken).<br/>
    /// The client SHOULD prioritize these hints over the numeric priorities,
    /// but MAY still use the priorities to select from ambiguous matches.
    /// </summary>
    [JsonPropertyName("hints")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ModelHint>? Hints { get; init; }

    /// <summary>
    /// 服务器希望客户端选择的模型与以下维度的一致性如何。<br/>
    /// 所有值必须介于 0 到 1 之间，代表该属性的相对重要性。<br/>
    /// 这些优先级不应被视为绝对标准，而应被视为互相权衡。<br/>
    /// 客户端可能会根据其偏好或其他因素调整其解释。<br/>
    /// How much the server wants the client-selected model to align with the following dimensions.<br/>
    /// All values must be between 0 and 1, representing the relative importance of that property.<br/>
    /// These priorities should not be treated as absolute standards,
    /// but as weighted trade-offs relative to each other.<br/>
    /// The client MAY adjust its interpretation based on its own preferences or other factors.
    /// </summary>
    [JsonPropertyName("costPriority")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? CostPriority { get; init; }

    /// <summary>
    /// 速度优先级（值介于 0 到 1 之间）<br/>
    /// Speed priority (value between 0 and 1)
    /// </summary>
    [JsonPropertyName("speedPriority")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? SpeedPriority { get; init; }

    /// <summary>
    /// 智能优先级（值介于 0 到 1 之间）<br/>
    /// Intelligence priority (value between 0 and 1)
    /// </summary>
    [JsonPropertyName("intelligencePriority")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? IntelligencePriority { get; init; }
}

/// <summary>
/// 用于模型选择的提示。<br/>
/// 这可用于请求特定的模型名称或模型系列。<br/>
/// A hint for model selection.<br/>
/// This can be used to request a specific model name or model family.
/// </summary>
public sealed record ModelHint
{
    /// <summary>
    /// 要匹配的模型名称。<br/>
    /// 客户端应将此作为子字符串或前缀匹配进行评估，并且不区分大小写。<br/>
    /// 例如：<br/>
    /// - "claude-3-5-sonnet-20241022" 应匹配该确切模型<br/>
    /// - "claude" 应匹配任何 Claude 模型<br/>
    /// 客户端还可以将字符串映射到不同提供商的模型名称或不同的模型系列，只要它填补了类似的利基；例如：<br/>
    /// - "gemini-1.5-flash" 可以匹配 "claude-3-haiku-20240307"<br/>
    /// A model name to match.<br/>
    /// The client SHOULD evaluate this as a substring or prefix match, case-insensitively.<br/>
    /// For example:<br/>
    /// - "claude-3-5-sonnet-20241022" should match that exact model<br/>
    /// - "claude" should match any Claude model<br/>
    /// The client MAY also map the string to a different provider's model name or a different
    /// model family, as long as it fills a similar niche; for example:<br/>
    /// - "gemini-1.5-flash" could match "claude-3-haiku-20240307"
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }
}

/// <summary>
/// 控制模型如何选择和使用工具。<br/>
/// Controls how the model selects and uses tools.
/// </summary>
public sealed record ToolChoice
{
    /// <summary>
    /// 工具选择模式：<br/>
    /// - "auto": 模型自动决定是否使用工具<br/>
    /// - "required": 模型必须使用至少一个工具<br/>
    /// - "none": 模型不得使用任何工具<br/>
    /// Tool selection mode:<br/>
    /// - "auto": Model decides whether to use tools<br/>
    /// - "required": Model must use at least one tool<br/>
    /// - "none": Model must not use any tools
    /// </summary>
    [JsonPropertyName("mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; init; }
}
