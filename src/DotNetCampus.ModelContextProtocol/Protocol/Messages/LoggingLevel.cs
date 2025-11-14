using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 日志消息的严重性。<br/>
/// 这些级别映射到 RFC-5424 中指定的 syslog 消息严重性。<br/>
/// The severity of a log message.<br/>
/// These map to syslog message severities, as specified in RFC-5424:
/// https://datatracker.ietf.org/doc/html/rfc5424#section-6.2.1
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LoggingLevel>))]
public enum LoggingLevel
{
    /// <summary>
    /// 详细调试信息<br/>
    /// Detailed debugging information
    /// </summary>
    Debug,

    /// <summary>
    /// 一般信息性消息<br/>
    /// General informational messages
    /// </summary>
    Info,

    /// <summary>
    /// 正常但重要的事件<br/>
    /// Normal but significant events
    /// </summary>
    Notice,

    /// <summary>
    /// 警告条件<br/>
    /// Warning conditions
    /// </summary>
    Warning,

    /// <summary>
    /// 错误条件<br/>
    /// Error conditions
    /// </summary>
    Error,

    /// <summary>
    /// 严重条件<br/>
    /// Critical conditions
    /// </summary>
    Critical,

    /// <summary>
    /// 必须立即采取行动<br/>
    /// Action must be taken immediately
    /// </summary>
    Alert,

    /// <summary>
    /// 系统不可用<br/>
    /// System is unusable
    /// </summary>
    Emergency
}
