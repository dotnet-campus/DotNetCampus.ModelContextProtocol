using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 从服务器发送以请求客户端的根目录列表。<br/>
/// 根目录允许服务器请求操作的特定目录或文件。<br/>
/// 根目录的常见示例是提供服务器应操作的一组存储库或目录。<br/>
/// 当服务器需要了解文件系统结构或访问客户端有权读取的特定位置时，通常使用此请求。<br/>
/// Sent from the server to request a list of root URIs from the client. Roots allow servers
/// to ask for specific directories or files to operate on. A common example for roots is
/// providing a set of repositories or directories a server should operate on.<br/>
/// This request is typically used when the server needs to understand the file system structure
/// or access specific locations that the client has permission to read from.
/// </summary>
public sealed record ListRootsRequestParams : RequestParams
{
}

/// <summary>
/// 客户端对服务器的 roots/list 请求的响应。<br/>
/// 此结果包含 Root 对象数组，每个对象表示服务器可以操作的根目录或文件。<br/>
/// The client's response to a roots/list request from the server.<br/>
/// This result contains an array of Root objects, each representing a root directory
/// or file that the server can operate on.
/// </summary>
public sealed record ListRootsResult : Result
{
    /// <summary>
    /// 根目录列表<br/>
    /// List of roots
    /// </summary>
    [JsonPropertyName("roots")]
    public required IReadOnlyList<Root> Roots { get; init; }
}

/// <summary>
/// 表示服务器可以操作的根目录或文件。<br/>
/// Represents a root directory or file that the server can operate on.
/// </summary>
public sealed record Root
{
    /// <summary>
    /// 标识根目录的 URI。现在必须以 file:// 开头。<br/>
    /// 此限制可能会在协议的未来版本中放宽，以允许其他 URI 方案。<br/>
    /// The URI identifying the root. This must start with file:// for now.<br/>
    /// This restriction may be relaxed in future versions of the protocol
    /// to allow other URI schemes.
    /// </summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    /// <summary>
    /// 根目录的可选名称。<br/>
    /// 这可用于为根目录提供人类可读的标识符，这可能对显示目的或在应用程序的其他部分中引用根目录有用。<br/>
    /// An optional name for the root. This can be used to provide a human-readable identifier
    /// for the root, which may be useful for display purposes or for referencing the root
    /// in other parts of the application.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>
    /// 元数据字段<br/>
    /// See <a href="https://modelcontextprotocol.io/specification/2025-06-18/basic/index#meta">
    /// General fields: _meta</a> for notes on _meta usage.
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? Meta { get; init; }
}
