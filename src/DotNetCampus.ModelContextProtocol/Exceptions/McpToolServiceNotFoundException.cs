namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当 MCP 服务器响应客户端调用工具请求时，如果工具所需的依赖服务未找到，则抛出此异常。
/// </summary>
public class McpToolServiceNotFoundException : ModelContextProtocolException
{
    public McpToolServiceNotFoundException(string serviceName)
        : base($"MCP tool service not found: {serviceName}")
    {
    }
}
