namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当 MCP 工具的实现代码发现调用方（主要是 AI）错误使用了工具时，抛出此异常以提示 AI 错误信息，必要时指导 AI 如何正确使用工具。
/// </summary>
public class McpToolUsageException : McpToolException
{
    /// <summary>
    /// 初始化 <see cref="McpToolUsageException"/> 类的新实例。
    /// </summary>
    public McpToolUsageException()
        : base("The MCP tool was used incorrectly.")
    {
    }

    /// <summary>
    /// 初始化 <see cref="McpToolUsageException"/> 类的新实例。
    /// </summary>
    /// <param name="message">错误消息。</param>
    public McpToolUsageException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化 <see cref="McpToolUsageException"/> 类的新实例。
    /// </summary>
    /// <param name="message">错误消息。</param>
    /// <param name="innerException">导致此异常的内部异常。</param>
    public McpToolUsageException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
