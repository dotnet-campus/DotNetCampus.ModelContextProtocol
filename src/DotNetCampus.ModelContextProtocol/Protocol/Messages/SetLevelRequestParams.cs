using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 从客户端发送到服务器以调整日志记录级别的请求参数。<br/>
/// A request from the client to the server, to enable or adjust logging.
/// </summary>
public record SetLevelRequestParams : RequestParams
{
    /// <summary>
    /// 客户端希望从服务器接收的日志级别。<br/>
    /// 服务器应将此级别及更高级别（即更严重）的所有日志作为 notifications/message 发送给客户端。<br/>
    /// The level of logging that the client wants to receive from the server.
    /// The server should send all logs at this level and higher (i.e., more severe) to
    /// the client as notifications/message.
    /// </summary>
    [JsonPropertyName("level")]
    [JsonRequired]
    public required LoggingLevel Level { get; init; }
}
