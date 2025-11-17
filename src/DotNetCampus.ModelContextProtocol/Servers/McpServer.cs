using System.Diagnostics;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP 服务器。
/// </summary>
public class McpServer
{
    /// <summary>
    /// 初始化 <see cref="McpServer"/> 类的新实例。
    /// </summary>
    public McpServer()
    {
        Handlers = new McpRequestHandlerRegistry(this);
    }

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
    /// 获取 MCP 服务器的处理程序集合。
    /// </summary>
    public McpRequestHandlerRegistry Handlers { get; }

    /// <summary>
    /// 获取 MCP 服务器的上下文信息。
    /// </summary>
    public required McpServerContext Context
    {
        get;
        init
        {
            var oldContext = field;
            oldContext?.Handlers = null;
            field = value;
            value.Handlers = Handlers;
        }
    }

    /// <summary>
    /// 获取用于处理 MCP 请求的传输集合。
    /// </summary>
    public required IReadOnlyList<HttpServerTransport> Transports { get; init; }

    /// <summary>
    /// 获取 MCP 服务器工具集合。
    /// </summary>
    public required IMcpServerToolsProvider Tools { get; init; }

    /// <summary>
    /// 启用调试模式。<br/>
    /// 在调试模式下，服务器可能会记录更多的日志信息以帮助调试，同时也可能会通过 MCP 协议向客户端报告异常的详细信息。
    /// </summary>
    public void EnableDebugMode()
    {
        Context.IsDebugMode = true;
    }

    /// <summary>
    /// 运行 MCP 服务器。此异步任务会一直运行，直到取消或程序退出为止。
    /// </summary>
    /// <param name="cancellationToken">用于取消运行的取消令牌。</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var runTasks = Transports
            .Select(t => t.StartAsync(cancellationToken))
            .ToList();
        await Task.WhenAll(runTasks);
    }
}
