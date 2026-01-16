using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

/// <summary>
/// JSON-RPC 错误<br/>
/// Error information for a failed JSON-RPC request.
/// </summary>
public record JsonRpcError
{
    /// <summary>
    /// 错误代码<br/>
    /// A number indicating the error type that occurred.
    /// </summary>
    [JsonPropertyName("code")]
    public required int Code { get; init; }

    /// <summary>
    /// 错误消息<br/>
    /// A short description of the error.
    /// The message SHOULD be limited to a concise single sentence.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// 额外的错误数据（可选）<br/>
    /// Additional information about the error.
    /// The value of this member is defined by the sender
    /// (e.g. detailed error information, nested errors etc.).
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; init; }
}
