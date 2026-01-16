using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

/// <summary>
/// JSON-RPC 通知（不需要响应）<br/>
/// A notification which does not expect a response.
/// </summary>
public record JsonRpcNotification : JsonRpcMessage
{
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
