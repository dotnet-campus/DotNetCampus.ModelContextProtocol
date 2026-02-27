using System.Diagnostics;
using DotNetCampus.Cli.Compiler;
using DotNetCampus.Logging;
using Microsoft.VisualBasic;

namespace DotNetCampus.SampleMcpServer;

/// <summary>
/// MCP 服务器的命令行参数。
/// </summary>
internal record CommandLineOptions
{
    /// <summary>
    /// 获取从 Main 方法开始就使用的命令行参数。
    /// </summary>
    [RawArguments]
    public required string[] MainArgs { get; init; }

    /// <summary>
    /// 指定日志记录的最低日志级别。
    /// </summary>
    /// <remarks>
    /// 在 debug 下默认是 <see cref="Trace"/>，在 release 下默认是 <see cref="Information"/>。
    /// </remarks>
    [Option]
    public LogLevel? LogLevel { get; init; }

    /// <summary>
    /// 指定是否等待调试器附加。
    /// </summary>
    [Option]
    public bool WaitForAttach { get; init; }

    /// <summary>
    /// 指定 MCP 服务器使用的传输方式。
    /// </summary>
    [Option]
    public McpTransport? Transport { get; init; }

    /// <summary>
    /// 指定 MCP 服务器使用 http 传输方式时的监听地址和端口。
    /// </summary>
    /// <remarks>
    /// 如需绑定一个随机可用端口，请不要传入此参数或使用参数 --urls http://127.0.0.1:0;http://[::1]:0<br/>
    /// 如需使用特定端口，可使用 --urls http://localhost:5229<br/>
    /// </remarks>
    [Option]
    public IReadOnlyList<string>? Urls { get; init; }

    /// <summary>
    /// 获取实际应该使用的传输方式。
    /// 如果命令行参数中没有没有指定传输方式，则根据是否是开机自启动来决定。
    /// </summary>
    /// <returns>实际应该使用的传输方式。</returns>
    public McpTransport GetTransport() => Transport ?? McpTransport.Stdio;
}

/// <summary>
/// MCP 传输方式。
/// </summary>
internal enum McpTransport
{
    /// <summary>
    /// 使用 stdio 标准输入输出流进行通信。
    /// </summary>
    Stdio,

    /// <summary>
    /// 使用 Streamable Http 进行通信。
    /// </summary>
    Http,
}
