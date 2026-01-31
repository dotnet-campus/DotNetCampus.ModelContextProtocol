namespace DotNetCampus.ModelContextProtocol.Transports.Stdio;

/// <summary>
/// STDIO 客户端传输层的配置选项，用于启动 MCP 服务器进程。
/// </summary>
public record StdioClientTransportOptions
{
    /// <summary>
    /// 要执行的命令或可执行文件路径。
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// 传递给命令的命令行参数列表。
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>
    /// 启动进程时设置的环境变量。
    /// </summary>
    public IDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>();
}
