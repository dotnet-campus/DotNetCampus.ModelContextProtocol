using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 工具调用请求参数<br/>
/// Used by the client to invoke a tool provided by the server.
/// </summary>
public sealed record CallToolRequestParams : RequestParams
{
    /// <summary>
    /// 要调用的工具名称<br/>
    /// The name of the tool to call
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 传递给工具的参数<br/>
    /// Arguments to pass to the tool
    /// </summary>
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; init; }
}
