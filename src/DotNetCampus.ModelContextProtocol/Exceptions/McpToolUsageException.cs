namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当 MCP 工具的实现代码发现调用方（主要是 AI）错误使用了工具时，抛出此异常以提示 AI 错误信息，必要时指导 AI 如何正确使用工具。
/// </summary>
public class McpToolUsageException : ModelContextProtocolException
{
    public McpToolUsageException(string message)
        : base(message)
    {
    }
}
