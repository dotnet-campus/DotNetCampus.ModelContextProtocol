namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// MCP 协议相关的异常基类。
/// </summary>
public class ModelContextProtocolException : Exception
{
    /// <summary>
    /// 初始化 <see cref="ModelContextProtocolException"/> 类的新实例。
    /// </summary>
    public ModelContextProtocolException()
    {
    }

    /// <summary>
    /// 初始化 <see cref="ModelContextProtocolException"/> 类的新实例。
    /// </summary>
    /// <param name="message">错误消息</param>
    public ModelContextProtocolException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化 <see cref="ModelContextProtocolException"/> 类的新实例。
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public ModelContextProtocolException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
