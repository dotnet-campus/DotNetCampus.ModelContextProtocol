using DotNetCampus.ModelContextProtocol.Hosting.Logging;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// 为 MCP 传输层的实现提供与 MCP 协议相关的上下文信息。
/// </summary>
public interface IServerTransportContext
{
    /// <summary>
    /// 专门提供给 MCP 库内部代码和外部扩展使用的日志记录器。
    /// </summary>
    IMcpLogger Logger { get; }

    /// <summary>
    /// 获取 MCP 服务器传输层管理器。
    /// </summary>
    IServerTransportManager Transport { get; }
}
