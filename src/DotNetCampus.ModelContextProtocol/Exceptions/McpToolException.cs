namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当 MCP 工具调用的实现中无法正确完成任务时，应抛出此异常。<br/>
/// 应优先选择更具体的异常类型，仅在难以将异常分类到更具体的异常类型时，才使用此异常。<br/>
/// 当直接引发此异常类型时，通常说明应用程序已陷入了某种状态，在这种状态下 AI 其实也难以通过修改用法自主修复这样的异常。
/// </summary>
public class McpToolException : ModelContextProtocolException
{
    /// <summary>
    /// 初始化 <see cref="McpToolException"/> 类的新实例。
    /// </summary>
    public McpToolException()
        : base("An error occurred while executing the MCP tool.")
    {
    }

    /// <summary>
    /// 初始化 <see cref="McpToolException"/> 类的新实例。
    /// </summary>
    /// <param name="message">错误消息。</param>
    public McpToolException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化 <see cref="McpToolException"/> 类的新实例。
    /// </summary>
    /// <param name="message">错误消息。</param>
    /// <param name="innerException">导致此异常的内部异常。</param>
    public McpToolException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
