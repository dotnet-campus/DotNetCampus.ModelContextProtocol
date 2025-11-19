using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当 MCP 服务器响应客户端读取资源请求时，如果指定的资源未找到，则抛出此异常。
/// </summary>
public class McpResourceNotFoundException : ModelContextProtocolException
{
    /// <summary>
    /// 初始化 <see cref="McpResourceNotFoundException"/> 类的新实例。
    /// </summary>
    /// <param name="context">读取资源的上下文。</param>
    public McpResourceNotFoundException(IMcpServerReadResourceContext context)
        : base($"MCP resource not found at: {context.Uri}")
    {
    }
}
