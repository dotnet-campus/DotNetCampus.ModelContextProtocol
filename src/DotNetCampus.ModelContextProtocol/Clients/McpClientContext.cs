using DotNetCampus.ModelContextProtocol.Clients.Transports;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;

namespace DotNetCampus.ModelContextProtocol.Clients;

/// <summary>
/// MCP 客户端的上下文信息。
/// </summary>
internal record McpClientContext : IClientTransportContext
{
    /// <inheritdoc />
    public required IMcpLogger Logger { get; init; }

    /// <summary>
    /// 为 MCP 客户端提供依赖注入。
    /// </summary>
    public IServiceProvider? ServiceProvider { get; internal init; }

    /// <summary>
    /// 用于与服务器通信的传输层管理器。
    /// </summary>
    public IClientTransportManager Transport
    {
        get => field ?? throw new InvalidOperationException("Transport 未被设置。");
        internal set => field = field switch
        {
            null => value,
            _ => throw new InvalidOperationException("Transport 已经被设置，不能重复设置。"),
        };
    }

    /// <summary>
    /// 指示是否启用调试模式。<br/>
    /// 启用后会记录或报告更多调试信息。
    /// </summary>
    public bool IsDebugMode { get; internal set; }
}
