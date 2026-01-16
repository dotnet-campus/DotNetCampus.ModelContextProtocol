using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

/// <summary>
/// JSON-RPC 响应<br/>
/// A successful (non-error) response to a request.
/// </summary>
public record JsonRpcResponse : JsonRpcMessage
{
    /// <summary>
    /// 无需响应标记<br/>
    /// A special marker indicating that no JSON response needs to be sent
    /// </summary>
    internal static JsonRpcResponse NoResponse { get; } = new() { Id = null };

    /// <summary>
    /// 是否为无需响应标记<br/>
    /// Indicates whether this is a no-response marker
    /// </summary>
    internal bool IsNoResponse => Id is null;

    /// <summary>
    /// 请求 ID<br/>
    /// The ID of the request this response corresponds to
    /// </summary>
    [JsonPropertyName("id")]
    public object? Id { get; init; }

    /// <summary>
    /// 成功响应结果<br/>
    /// The result of a successful request
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; init; }

    /// <summary>
    /// 错误信息（与 result 互斥）<br/>
    /// Error object if the request failed
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; init; }
}
