using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 表示客户端用于调用服务器提供的工具的 <see cref="RequestMethods.ToolsCall"/> 请求的参数。<br/>
/// The parameters used with a <see cref="RequestMethods.ToolsCall"/> request from a client to invoke a tool provided by the server.
/// </summary>
public sealed record CallToolRequestParams : TaskAugmentedRequestParams
{
    /// <summary>
    /// 来自 MCP 协议中 tools/call 请求中 name 字段的工具名称。<br/>
    /// The name of the tool to call from the name field in the tools/call request in the MCP protocol.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 来自 MCP 协议中 tools/call 请求中 arguments 字段的 JSON 元素。<br/>
    /// JSON element from the arguments field in the tools/call request in the MCP protocol.
    /// </summary>
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; init; }
}
