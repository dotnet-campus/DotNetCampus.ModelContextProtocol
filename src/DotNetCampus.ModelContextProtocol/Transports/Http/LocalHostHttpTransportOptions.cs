using System.Diagnostics.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// HTTP 传输层配置选项。
/// </summary>
public record LocalHostHttpTransportOptions
{
    /// <summary>
    /// 指定用于传输的端口号。
    /// </summary>
    public int Port { get; init; } = 5000;

    /// <summary>
    /// 指定用于传输的端点。
    /// </summary>
    public string EndPoint
    {
        get => field ??= "/mcp";
        init => field = value.StartsWith('/') ? value : "/" + value;
    }

    /// <summary>
    /// 是否使用无状态会话模式。<br/>
    /// MCP 协议要求全双工通信，为了使 HTTP 支持这一点，MCP 服务器需要保留一个长连接；这就要求服务器维护会话状态。<br/>
    /// 但如果你的应用场景允许，可以选择无状态会话模式；此时客户端无需提前连接即可使用此 MCP 服务器的功能，但所有服务端向客户端的发送消息的功能都将被禁用。
    /// </summary>
    public bool Stateless { get; init; }

    /// <summary>
    /// 指定是否兼容旧的 SSE传输层协议（2024-11-05）。
    /// </summary>
    [MemberNotNullWhen(true, nameof(SseEndPoint), nameof(SseMessageEndPoint))]
    public bool IsCompatibleWithSse { get; init; }

    /// <summary>
    /// SSE endpoint - 用于旧协议 HTTP+SSE (2024-11-05) 兼容
    /// </summary>
    public string? SseEndPoint => IsCompatibleWithSse ? $"{EndPoint}/sse" : null;

    /// <summary>
    /// Message endpoint - 用于旧协议 HTTP+SSE (2024-11-05) 兼容
    /// </summary>
    public string? SseMessageEndPoint => IsCompatibleWithSse ? $"{EndPoint}/messages" : null;

    /// <summary>
    /// 指定用于传输的基础 URL。
    /// </summary>
    public IReadOnlyList<string> GetUrlPrefixes()
    {
        return [$"http://localhost:{Port}/", $"http://127.0.0.1:{Port}/", $"http://[::1]:{Port}/"];
    }
}
