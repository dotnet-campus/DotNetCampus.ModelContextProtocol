using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 向客户端描述工具的额外属性。<br/>
/// 注意：ToolAnnotations 中的所有属性都是提示。<br/>
/// 它们不保证提供对工具行为的忠实描述（包括像 title 这样的描述性属性）。<br/>
/// 客户端不应基于从不可信服务器收到的 ToolAnnotations 来做出工具使用决策。<br/>
/// Additional properties describing a Tool to clients.<br/>
/// NOTE: all properties in ToolAnnotations are **hints**.<br/>
/// They are not guaranteed to provide a faithful description of tool behavior
/// (including descriptive properties like title).<br/>
/// Clients should never make tool use decisions based on ToolAnnotations
/// received from untrusted servers.
/// </summary>
public sealed record ToolAnnotations
{
    /// <summary>
    /// 工具的人类可读标题。<br/>
    /// A human-readable title for the tool.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// 如果为 true，则工具可能对其环境执行破坏性更新。<br/>
    /// 如果为 false，则工具仅执行递增更新。<br/>
    /// （此属性仅在 readOnlyHint == false 时才有意义）<br/>
    /// 默认值：false<br/>
    /// If true, the tool may perform destructive updates to its environment.
    /// If false, the tool performs only additive updates.<br/>
    /// (This property is meaningful only when readOnlyHint == false)<br/>
    /// Default: false
    /// </summary>
    [JsonPropertyName("destructiveHint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DestructiveHint { get; init; }

    /// <summary>
    /// 如果为 true，则工具是幂等的（多次调用相同参数产生相同结果）。<br/>
    /// 默认值：false<br/>
    /// If true, the tool is idempotent
    /// (multiple calls with the same parameters produce the same result).<br/>
    /// Default: false
    /// </summary>
    [JsonPropertyName("idempotentHint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IdempotentHint { get; init; }

    /// <summary>
    /// 如果为 true，则工具可能产生不在 outputSchema 中定义的输出。<br/>
    /// 默认值：false<br/>
    /// If true, the tool may produce output not defined in the outputSchema.<br/>
    /// Default: false
    /// </summary>
    [JsonPropertyName("openWorldHint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? OpenWorldHint { get; init; }

    /// <summary>
    /// 如果为 true，则工具不会修改其环境。<br/>
    /// 默认值：false<br/>
    /// If true, the tool does not modify its environment.<br/>
    /// Default: false
    /// </summary>
    [JsonPropertyName("readOnlyHint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReadOnlyHint { get; init; }
}
