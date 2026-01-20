using DotNetCampus.ModelContextProtocol.Hosting.Logging;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// MCP 客户端传输层的上下文信息。
/// </summary>
public interface IClientTransportContext
{
    /// <summary>
    /// 获取 MCP 日志记录器。
    /// </summary>
    IMcpLogger Logger { get; }
}
