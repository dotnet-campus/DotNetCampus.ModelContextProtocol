namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当 MCP 服务器收到调用 MCP 工具的请求时，如果缺少必需的参数，则抛出此异常。
/// </summary>
public class McpToolMissingRequiredArgumentException : ModelContextProtocolException
{
    /// <summary>
    /// 初始化 <see cref="McpToolMissingRequiredArgumentException"/> 类的新实例。
    /// </summary>
    /// <param name="argumentName">缺少的参数名称。</param>
    public McpToolMissingRequiredArgumentException(string argumentName)
        : base($"Missing required argument: {argumentName}")
    {
    }
}
