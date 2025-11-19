using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 描述 MCP 实现的名称和版本，带有用于 UI 表示的可选标题。<br/>
/// Describes the name and version of an MCP implementation,
/// with an optional title for UI representation.
/// </summary>
public sealed record Implementation : IBaseMetadata
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
    /// 版本号<br/>
    /// Version number
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>
    /// 图标列表<br/>
    /// List of icons
    /// </summary>
    [JsonPropertyName("icons")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<Icon>? Icons { get; init; }

    /// <summary>
    /// 网站 URL<br/>
    /// Website URL
    /// </summary>
    [JsonPropertyName("websiteUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WebsiteUrl { get; init; }
}
