using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 对话中消息和数据的发送者或接收者。<br/>
/// The sender or recipient of messages and data in a conversation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Role>))]
public enum Role
{
    /// <summary>
    /// 用户角色<br/>
    /// User role
    /// </summary>
    [JsonStringEnumMemberName("user")]
    User,

    /// <summary>
    /// 助手角色<br/>
    /// Assistant role
    /// </summary>
    [JsonStringEnumMemberName("assistant")]
    Assistant,
}
