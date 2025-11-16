namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当 MCP 服务器收到调用 MCP 工具的请求时，如果缺少必需的参数，则抛出此异常。
/// </summary>
public class McpToolMissingRequiredArgumentException : ModelContextProtocolException
{
    public McpToolMissingRequiredArgumentException(string argumentName)
        : base($"Missing required argument: {argumentName}")
    {
    }
}
