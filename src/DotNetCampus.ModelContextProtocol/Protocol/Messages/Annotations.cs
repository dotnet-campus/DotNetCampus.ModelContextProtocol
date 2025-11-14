using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// 客户端的可选注解。客户端可以使用注解来告知对象或数据的使用或显示方式。
/// </summary>
public sealed record Annotations
{
    /// <summary>
    /// 描述此对象或数据的预期受众。
    /// 可以包含多个条目以指示对多个受众有用的内容（例如 ["user", "assistant"]）。
    /// </summary>
    [JsonPropertyName("audience")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Audience { get; init; }

    /// <summary>
    /// 描述此数据对于服务器操作的重要程度。
    /// 值 1 表示"最重要"，表示数据实际上是必需的，值 0 表示"最不重要"，表示数据完全是可选的。
    /// 范围：0 到 1 之间的数字。
    /// </summary>
    [JsonPropertyName("priority")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Priority { get; init; }

    /// <summary>
    /// 资源最后一次修改的时刻，采用 ISO 8601 格式的字符串。
    /// 应为 ISO 8601 格式的字符串（例如 "2025-01-12T15:00:58Z"）。
    /// 示例：打开文件中的最后活动时间戳，资源附加时的时间戳等。
    /// </summary>
    [JsonPropertyName("lastModified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastModified { get; init; }
}
