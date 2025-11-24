namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// 传输层上下文接口。<br/>
/// 用于多对一传输层（如 HTTP）识别客户端，传递会话信息。<br/>
/// Transport context interface.<br/>
/// Used by many-to-one transports (e.g., HTTP) to identify clients and pass session information.
/// </summary>
public interface ITransportContext
{
    /// <summary>
    /// 会话 ID（可选）<br/>
    /// Session ID (optional)
    /// </summary>
    string? SessionId { get; }
}
