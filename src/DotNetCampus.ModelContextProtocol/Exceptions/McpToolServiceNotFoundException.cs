namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当 MCP 服务器响应客户端调用工具请求时，如果工具所需的依赖服务未找到，则抛出此异常。
/// </summary>
public class McpToolServiceNotFoundException : McpToolException
{
    /// <summary>
    /// 初始化 <see cref="McpToolServiceNotFoundException"/> 类的新实例。
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    public McpToolServiceNotFoundException(string serviceName)
        : base($"MCP tool service not found: {serviceName}")
    {
    }
}
