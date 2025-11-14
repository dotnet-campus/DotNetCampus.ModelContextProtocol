using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 图标信息<br/>
/// Icon information
/// </summary>
public sealed record Icon
{
    /// <summary>
    /// 图标源 URL<br/>
    /// Icon source URL
    /// </summary>
    [JsonPropertyName("src")]
    public required string Source { get; init; }

    /// <summary>
    /// 图标的 MIME 类型<br/>
    /// MIME type of the icon
    /// </summary>
    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; init; }

    /// <summary>
    /// 图标尺寸列表<br/>
    /// List of icon sizes
    /// </summary>
    [JsonPropertyName("sizes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<string>? Sizes { get; init; }

    /// <summary>
    /// 图标主题<br/>
    /// Icon theme
    /// </summary>
    [JsonPropertyName("theme")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Theme { get; init; }
}
