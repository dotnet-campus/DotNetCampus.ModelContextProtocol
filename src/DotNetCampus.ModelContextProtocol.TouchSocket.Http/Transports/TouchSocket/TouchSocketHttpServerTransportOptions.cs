using System.Diagnostics.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Transports.Http;

namespace DotNetCampus.ModelContextProtocol.Transports.TouchSocket;

/// <summary>
/// TouchSocket HTTP 服务端传输层配置选项。
/// </summary>
/// <remarks>
/// TouchSocket.Http 的服务端传输层暂时没考虑兼容旧的 SSE 传输层协议（2024-11-05），
/// 若要兼容 SSE，请使用 MCP 库自带的 <see cref="LocalHostHttpServerTransport"/> 传输层。
/// </remarks>
public record TouchSocketHttpServerTransportOptions
{
    /// <summary>
    /// 指定监听的主机和端口列表。
    /// <code>
    /// // 监听格式："IPv4:端口", "IPv6:端口"
    /// [$"127.0.0.1:{Port}", $"[::1]:{Port}"]
    /// [$"0.0.0.0:{Port}", $"[::]:{Port}"]
    /// // 可监听 1 个或多个地址，也可以有各自不同的端口号。
    /// </code>
    /// </summary>
    /// <remarks>
    /// 只能使用IP地址和端口号进行监听，不能使用域名。
    /// </remarks>
    public required IReadOnlyList<string> Listen { get; init; }

    /// <summary>
    /// 指定用于传输的端点。
    /// </summary>
    [AllowNull]
    public string EndPoint
    {
        get => field ??= "/mcp";
        init => field = value switch
        {
            null => null,
            _ => value.StartsWith('/') ? value : "/" + value,
        };
    }
}
