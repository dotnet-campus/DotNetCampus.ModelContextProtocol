namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP 服务器。
/// </summary>
public class McpServer
{
    private readonly IReadOnlyList<HttpServerTransport> _transports;

    /// <summary>
    /// 初始化 <see cref="McpServer"/> 类的新实例。
    /// </summary>
    /// <param name="transports">用于处理 MCP 请求的传输集合。</param>
    public McpServer(IReadOnlyList<HttpServerTransport> transports)
    {
        _transports = transports;
    }

    /// <summary>
    /// 运行 MCP 服务器。此异步任务会一直运行，直到取消或程序退出为止。
    /// </summary>
    /// <param name="cancellationToken">用于取消运行的取消令牌。</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var runTasks = _transports
            .Select(t => t.StartAsync(cancellationToken))
            .ToList();
        await Task.WhenAll(runTasks);
    }
}
