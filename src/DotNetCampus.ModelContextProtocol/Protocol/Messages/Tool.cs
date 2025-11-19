using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 工具定义<br/>
/// Definition of a tool the server can call
/// </summary>
public sealed record Tool : IBaseMetadata
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
    /// 如果未提供，应使用 name 作为显示名称（针对 Tool，如果存在 annotations.title，应优先使用它而不是 name）。<br/>
    /// Intended for UI and end-user contexts — optimized to be human-readable
    /// and easily understood, even by those unfamiliar with domain-specific terminology.<br/>
    /// If not provided, the name should be used for display
    /// (except for Tool, where annotations.title should be given precedence over using name, if present).
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// 该工具的作用描述。<br/>
    /// 客户端可以使用这些信息来改善 LLM 对可用工具的理解。<br/>
    /// 可以将其视为给模型的"提示"。<br/>
    /// A description of what this tool does.<br/>
    /// This can be used by clients to improve the LLM's understanding of available tools.
    /// It can be thought of like a "hint" to the model.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// 定义工具预期参数的 JSON Schema 对象。<br/>
    /// A JSON Schema object defining the expected parameters for the tool.
    /// </summary>
    [JsonPropertyName("inputSchema")]
    public required JsonElement InputSchema { get; init; }

    /// <summary>
    /// 可选的 JSON Schema 对象，定义工具输出的结构，
    /// 该输出将在 CallToolResult 的 structuredContent 字段中返回。<br/>
    /// An optional JSON Schema object defining the structure of the tool's output
    /// returned in the structuredContent field of a CallToolResult.
    /// </summary>
    [JsonPropertyName("outputSchema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? OutputSchema { get; init; }

    /// <summary>
    /// 可选的额外工具信息。<br/>
    /// 显示名称的优先级顺序是：title、annotations.title、然后是 name。<br/>
    /// Optional additional tool information.<br/>
    /// Display name precedence order is: title, annotations.title, then name.
    /// </summary>
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolAnnotations? Annotations { get; init; }

    /// <summary>
    /// 图标列表<br/>
    /// List of icons
    /// </summary>
    [JsonPropertyName("icons")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<Icon>? Icons { get; init; }

    /// <summary>
    /// 元数据字段<br/>
    /// See <a href="https://modelcontextprotocol.io/specification/2025-06-18/basic/index#meta">
    /// General fields: _meta</a> for notes on _meta usage.
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? Meta { get; init; }
}