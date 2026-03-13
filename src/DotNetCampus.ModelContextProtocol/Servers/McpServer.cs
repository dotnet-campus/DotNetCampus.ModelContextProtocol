using DotNetCampus.ModelContextProtocol.Transports;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP 服务器。
/// </summary>
public class McpServer
{
    private readonly McpServerContext _context;

    /// <summary>
    /// 初始化 <see cref="McpServer"/> 类的新实例。
    /// </summary>
    /// <param name="context">MCP 服务器的上下文信息。</param>
    internal McpServer(McpServerContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取 MCP 服务端传输层管理器的实现。
    /// </summary>
    private ServerTransportManager Transport => (ServerTransportManager)_context.Transport;

    /// <summary>
    /// 获取 MCP 服务器使用的传输层列表。
    /// </summary>
    /// <remarks>
    /// 一般的 MCP 服务器都会添加且只添加一个传输层，这是因为 MCP 协议要求必须实现的两个传输层 Stdio 和 StreamableHttp 在业务上就很难同时工作。<br/>
    /// 但是，MCP 协议同时又允许实现其他的传输层，而其他传输层则可能与 StreamableHttp 共存；在这种情况下，MCP 服务器就可能会包含多个传输层。
    /// </remarks>
    public IReadOnlyList<IServerTransport> Transports => Transport.Transports;

    /// <summary>
    /// 获取或初始化服务器名称。
    /// </summary>
    public required string ServerName { get; init; }

    /// <summary>
    /// 获取或初始化服务器版本。
    /// </summary>
    public required string ServerVersion { get; init; }

    /// <summary>
    /// 获取或初始化服务器使用说明(可选)。
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// 获取 MCP 服务器的上下文信息。
    /// </summary>
    public IMcpServerContext Context => _context;

    /// <summary>
    /// 获取 MCP 服务器工具集合。
    /// </summary>
    public required IMcpServerToolsProvider Tools { get; init; }

    /// <summary>
    /// 获取 MCP 服务器资源集合。
    /// </summary>
    public required IMcpServerResourcesProvider Resources { get; init; }

    /// <summary>
    /// 启用调试模式。<br/>
    /// 在调试模式下，服务器可能会记录更多的日志信息以帮助调试，同时也可能会通过 MCP 协议向客户端报告异常的详细信息。
    /// </summary>
    public void EnableDebugMode()
    {
        _context.IsDebugMode = true;
    }

    /// <summary>
    /// 运行 MCP 服务器。此异步任务会一直运行，直到取消令牌取消、调用 <see cref="StopAsync"/> 或程序退出为止。
    /// </summary>
    /// <param name="cancellationToken">用于取消运行的取消令牌。</param>
    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        return Transport.RunAsync(cancellationToken);
    }

    /// <summary>
    /// 运行 MCP 服务器。当服务启动完成后，此异步任务即返回；而后 MCP 服务器一直保持运行。
    /// </summary>
    /// <param name="cancellationToken">用于取消启动的取消令牌。当启动完成后，此取消令牌不会影响 MCP 服务器的运行。</param>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Transport.StartAsync(cancellationToken);
    }

    /// <summary>
    /// 停止 MCP 服务器。当服务停止后，此异步任务即返回。
    /// </summary>
    /// <param name="cancellationToken">用于取消停止的取消令牌。</param>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Transport.StopAsync(cancellationToken);
    }
}
