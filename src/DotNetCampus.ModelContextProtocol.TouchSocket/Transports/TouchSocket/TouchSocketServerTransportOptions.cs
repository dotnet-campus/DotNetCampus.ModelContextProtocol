using System.Diagnostics.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.TouchSocket.Transports.TouchSocket;

/// <summary>
/// TouchSocket HTTP 服务端传输层配置选项。
/// </summary>
public record TouchSocketServerTransportOptions
{
    /// <summary>
    /// 指定监听的主机和端口列表。
    /// <code>
    /// // 监听格式："域名:端口", "IPv4:端口", "IPv6:端口"
    /// [$"localhost:{Port}", $"127.0.0.1:{Port}", $"[::1]:{Port}"]
    /// [$"example.com:{Port}", $"0.0.0.0:{Port}", $"[::]:{Port}"]
    /// // 可监听 1 个或多个地址，也可以有各自不同的端口号。
    /// </code>
    /// </summary>
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

    /// <summary>
    /// 指定是否兼容旧的 SSE传输层协议（2024-11-05）。默认为 <see langword="false"/>。
    /// </summary>
    [MemberNotNullWhen(true, nameof(SseEndPoint), nameof(SseMessageEndPoint))]
    public bool IsCompatibleWithSse { get; init; }

    /// <summary>
    /// SSE endpoint - 用于旧协议 HTTP+SSE (2024-11-05) 兼容。
    /// </summary>
    /// <remarks>
    /// 仅在 <see cref="IsCompatibleWithSse"/> 为 <see langword="true"/> 时生效。
    /// </remarks>
    public string? SseEndPoint => IsCompatibleWithSse ? $"{EndPoint}/sse" : null;

    /// <summary>
    /// Message endpoint - 用于旧协议 HTTP+SSE (2024-11-05) 兼容。
    /// </summary>
    /// <remarks>
    /// 仅在 <see cref="IsCompatibleWithSse"/> 为 <see langword="true"/> 时生效。
    /// </remarks>
    public string? SseMessageEndPoint => IsCompatibleWithSse ? $"{EndPoint}/messages" : null;
}
