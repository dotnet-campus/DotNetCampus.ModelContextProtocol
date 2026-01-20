namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 因 MCP 服务端问题导致的异常。<br/>
/// 此异常只会在 MCP 客户端（注意不是服务端）内部引发，表示 MCP 服务端出现了 MCP 协议之外的异常。
/// </summary>
public class McpServerException : ModelContextProtocolException
{
    /// <summary>
    /// 初始化 <see cref="McpServerException"/> 类的新实例。
    /// </summary>
    public McpServerException()
    {
    }

    /// <summary>
    /// 初始化 <see cref="McpServerException"/> 类的新实例。
    /// </summary>
    /// <param name="message">错误消息</param>
    public McpServerException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化 <see cref="McpServerException"/> 类的新实例。
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public McpServerException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
