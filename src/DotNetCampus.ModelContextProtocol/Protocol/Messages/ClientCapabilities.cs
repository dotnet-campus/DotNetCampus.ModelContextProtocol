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
    public object? Sampling { get; init; }

    /// <summary>
    /// 如果存在，表示客户端支持从服务器引出信息。<br/>
    /// Present if the client supports elicitation from the server.
    /// </summary>
    [JsonPropertyName("elicitation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Elicitation { get; init; }

    /// <summary>
    /// 客户端支持的实验性、非标准能力。<br/>
    /// Experimental, non-standard capabilities that the client supports.
    /// </summary>
    [JsonPropertyName("experimental")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Experimental { get; init; }
}
