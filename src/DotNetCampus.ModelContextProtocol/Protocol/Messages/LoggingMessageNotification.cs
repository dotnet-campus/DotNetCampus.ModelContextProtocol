using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 从服务器传递给客户端的日志消息通知。<br/>
/// 如果客户端尚未发送 logging/setLevel 请求，服务器可以决定自动发送哪些消息。<br/>
/// Notification of a log message passed from server to client. If no
/// logging/setLevel request has been sent from the client, the server MAY decide which
/// messages to send automatically.
/// </summary>
public record LoggingMessageNotification : JsonRpcNotification
{
    /// <summary>
    /// 通知参数<br/>
    /// Notification parameters
    /// </summary>
    [JsonPropertyName("params")]
    [JsonRequired]
    public required new LoggingMessageParams Params { get; init; }
}

/// <summary>
/// 日志消息通知的参数<br/>
/// Parameters for logging message notification
/// </summary>
public record LoggingMessageParams
{
    /// <summary>
    /// 此日志消息的严重性。<br/>
    /// The severity of this log message.
    /// </summary>
    [JsonPropertyName("level")]
    [JsonRequired]
    public required LoggingLevel Level { get; init; }

    /// <summary>
    /// 发出此消息的日志记录器的可选名称。<br/>
    /// An optional name of the logger issuing this message.
    /// </summary>
    [JsonPropertyName("logger")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Logger { get; init; }

    /// <summary>
    /// 要记录的数据，例如字符串消息或对象。<br/>
    /// 此处允许任何 JSON 可序列化类型。<br/>
    /// The data to be logged, such as a string message or an object. Any JSON
    /// serializable type is allowed here.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonRequired]
    public required object Data { get; init; }
}
