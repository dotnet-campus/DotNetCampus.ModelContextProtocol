using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 客户端可能支持的能力。<br/>
/// 已知的能力在此架构中定义，但这不是一个封闭集：
/// 任何客户端都可以定义自己的额外能力。<br/>
/// Capabilities a client may support.
/// Known capabilities are defined here, in this schema,
/// but this is not a closed set: any client can define its own, additional capabilities.
/// </summary>
public record ClientCapabilities
{
    /// <summary>
    /// 如果存在，表示客户端支持列出根目录。<br/>
    /// Present if the client supports listing roots.
    /// </summary>
    [JsonPropertyName("roots")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RootsCapability? Roots { get; init; }

    /// <summary>
    /// 如果存在，表示客户端支持从 LLM 进行采样。<br/>
    /// Present if the client supports sampling from an LLM.
    /// </summary>
    [JsonPropertyName("sampling")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SamplingCapability? Sampling { get; init; }

    /// <summary>
    /// 如果存在，表示客户端支持从服务器引出信息。<br/>
    /// Present if the client supports elicitation from the server.
    /// </summary>
    [JsonPropertyName("elicitation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ElicitationCapability? Elicitation { get; init; }

    /// <summary>
    /// 如果存在，表示客户端支持任务系统。<br/>
    /// Present if the client supports task system.
    /// </summary>
    [JsonPropertyName("tasks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TasksClientCapability? Tasks { get; init; }

    /// <summary>
    /// 客户端支持的实验性、非标准能力。<br/>
    /// Experimental, non-standard capabilities that the client supports.
    /// </summary>
    [JsonPropertyName("experimental")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Experimental { get; init; }
}

/// <summary>
/// 客户端采样能力。<br/>
/// Client sampling capability.
/// </summary>
public sealed record SamplingCapability
{
    /// <summary>
    /// 支持上下文包含。<br/>
    /// Support for context inclusion.
    /// </summary>
    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Context { get; init; }

    /// <summary>
    /// 支持工具调用。<br/>
    /// Support for tool calling.
    /// </summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Tools { get; init; }
}

/// <summary>
/// 客户端引出能力。<br/>
/// Client elicitation capability.
/// </summary>
public sealed record ElicitationCapability
{
    /// <summary>
    /// 支持 Form 模式引出。<br/>
    /// Support for form mode elicitation.
    /// </summary>
    [JsonPropertyName("form")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Form { get; init; }

    /// <summary>
    /// 支持 URL 模式引出。<br/>
    /// Support for URL mode elicitation.
    /// </summary>
    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Url { get; init; }
}
