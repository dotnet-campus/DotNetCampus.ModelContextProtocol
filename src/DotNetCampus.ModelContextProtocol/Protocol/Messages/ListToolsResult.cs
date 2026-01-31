using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 列出工具响应结果<br/>
/// The server's response to a tools/list request from the client.
/// </summary>
public sealed record ListToolsResult : PaginatedResult
{
    /// <summary>
    /// 工具列表<br/>
    /// List of tools
    /// </summary>
    [JsonPropertyName("tools")]
    public IReadOnlyList<Tool> Tools { get; init; } = [];
}
