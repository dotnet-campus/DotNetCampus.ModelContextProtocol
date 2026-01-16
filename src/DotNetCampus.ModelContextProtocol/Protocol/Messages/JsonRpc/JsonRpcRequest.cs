using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

/// <summary>
/// JSON-RPC 请求<br/>
/// A request that expects a response.
/// </summary>
public record JsonRpcRequest : JsonRpcMessage
{
    /// <summary>
    /// 请求 ID（字符串或数字）<br/>
    /// A uniquely identifying ID for a request in JSON-RPC.
    /// </summary>
    [JsonPropertyName("id")]
    public object? Id { get; init; }

    /// <summary>
    /// 方法名<br/>
    /// The name of the method to be invoked
    /// </summary>
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    /// <summary>
    /// 参数（可选）<br/>
    /// Optional parameters for the method
    /// </summary>
    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; init; }
}
