using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// MCP 工具调用请求参数。
/// </summary>
public sealed record CallToolRequestParams : RequestParams
{
    /// <summary>
    /// 获取或设置要调用的工具名称。
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置传递给工具的参数。
    /// </summary>
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; init; }
}
