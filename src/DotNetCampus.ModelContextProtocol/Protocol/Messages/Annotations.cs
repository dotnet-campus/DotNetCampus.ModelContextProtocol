using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 客户端的可选注解。客户端可以使用注解来告知对象或数据的使用或显示方式。<br/>
/// Optional annotations for the client.
/// The client can use annotations to inform how objects are used or displayed.
/// </summary>
public sealed record Annotations
{
    /// <summary>
    /// 描述此对象或数据的预期受众。<br/>
    /// 可以包含多个条目以指示对多个受众有用的内容（例如 ["user", "assistant"]）。<br/>
    /// Describes who the intended customer of this object or data is.<br/>
    /// It can include multiple entries to indicate content useful for multiple audiences
    /// (e.g., ["user", "assistant"]).
    /// </summary>
    [JsonPropertyName("audience")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Audience { get; init; }

    /// <summary>
    /// 描述此数据对于服务器操作的重要程度。<br/>
    /// 值 1 表示"最重要"，表示数据实际上是必需的，而 0 表示"最不重要"，表示数据完全是可选的。<br/>
    /// 范围：0 到 1 之间的数字。<br/>
    /// Describes how important this data is for operating the server.<br/>
    /// A value of 1 means "most important," and indicates that the data is effectively required,
    /// while 0 means "least important," and indicates that the data is entirely optional.<br/>
    /// Range: A number between 0 and 1.
    /// </summary>
    [JsonPropertyName("priority")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Priority { get; init; }

    /// <summary>
    /// 资源最后一次修改的时刻，采用 ISO 8601 格式的字符串。<br/>
    /// 应为 ISO 8601 格式的字符串（例如 "2025-01-12T15:00:58Z"）。<br/>
    /// 示例：打开文件中的最后活动时间戳，资源附加时的时间戳等。<br/>
    /// The moment the resource was last modified, as an ISO 8601 formatted string.<br/>
    /// Should be an ISO 8601 formatted string (e.g., "2025-01-12T15:00:58Z").<br/>
    /// Examples: last activity timestamp in an open file,
    /// timestamp when the resource was attached, etc.
    /// </summary>
    [JsonPropertyName("lastModified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastModified { get; init; }
}
