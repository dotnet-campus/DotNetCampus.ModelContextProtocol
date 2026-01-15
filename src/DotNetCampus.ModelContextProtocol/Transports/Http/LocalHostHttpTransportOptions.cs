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
    /// 按照 MCP 官方协议规范对传输层的要求：<br/>
    /// 服务器必须验证所有传入连接的 Origin 标头，以防止 DNS 重绑定攻击。<br/>
    /// Servers MUST validate the Origin header on all incoming connections to prevent DNS rebinding attacks<br/>
    /// 当启用时，会验证 Host 和 Origin header，拒绝非本机来源的请求。<br/>
    /// 默认值：true（推荐）。
    /// </summary>
    public bool EnableDnsRebindingProtection { get; init; } = true;

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
