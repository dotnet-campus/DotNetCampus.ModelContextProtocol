namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当应用程序的状态不满足，或发生了其他问题，导致 MCP 工具不能正确完成任务，则抛出此异常以提示 AI / 智能体任务失败，且 AI 不能自主修复。
/// </summary>
public class McpCallToolFailedException : ModelContextProtocolException
{
    /// <summary>
    /// 初始化 <see cref="McpCallToolFailedException"/> 类的新实例。
    /// </summary>
    public McpCallToolFailedException()
        : base("The MCP tool call failed.")
    {
    }

    /// <summary>
    /// 初始化 <see cref="McpCallToolFailedException"/> 类的新实例。
    /// </summary>
    /// <param name="message">错误消息。</param>
    public McpCallToolFailedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化 <see cref="McpCallToolFailedException"/> 类的新实例。
    /// </summary>
    /// <param name="message">错误消息。</param>
    /// <param name="innerException">导致此异常的内部异常。</param>
    public McpCallToolFailedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
