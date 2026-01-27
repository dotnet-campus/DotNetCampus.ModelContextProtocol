using System.Diagnostics.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.TouchSocket.Transports.TouchSocket;

/// <summary>
/// TouchSocket HTTP 传输层配置选项。<br/>
/// 支持监听 0.0.0.0 等所有网络接口。
/// </summary>
public record TouchSocketServerTransportOptions
{
    /// <summary>
    /// 指定用于传输的端口号。
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// 指定监听的 IP 地址。<br/>
    /// 默认值：0.0.0.0（监听所有网络接口）。
    /// </summary>
    public string Host { get; init; } = "0.0.0.0";

    /// <summary>
    /// 指定用于传输的端点。
    /// </summary>
    public string EndPoint
    {
        get => field ??= "/mcp";
        init => field = value.StartsWith('/') ? value : "/" + value;
    }

    /// <summary>
    /// 按照 MCP 官方协议规范对传输层的要求：<br/>
    /// 服务器必须验证所有传入连接的 Origin 标头，以防止 DNS 重绑定攻击。<br/>
    /// Servers MUST validate the Origin header on all incoming connections to prevent DNS rebinding attacks<br/>
    /// 当启用时，会验证 Host 和 Origin header，拒绝非本机来源的请求。<br/>
    /// 默认值：false（因为支持 0.0.0.0，通常用于跨网络访问）。
    /// </summary>
    public bool EnableDnsRebindingProtection { get; init; } = false;

    /// <summary>
    /// 指定是否兼容旧的 SSE 传输层协议（2024-11-05）。
    /// </summary>
    [MemberNotNullWhen(true, nameof(SseEndPoint), nameof(SseMessageEndPoint))]
    public bool IsCompatibleWithSse { get; init; }

    /// <summary>
    /// SSE endpoint - 用于旧协议 HTTP+SSE (2024-11-05) 兼容。
    /// </summary>
    public string? SseEndPoint => IsCompatibleWithSse ? $"{EndPoint}/sse" : null;

    /// <summary>
    /// Message endpoint - 用于旧协议 HTTP+SSE (2024-11-05) 兼容。
    /// </summary>
    public string? SseMessageEndPoint => IsCompatibleWithSse ? $"{EndPoint}/messages" : null;

    /// <summary>
    /// 获取监听的 URL 地址。
    /// </summary>
    public string GetUrl()
    {
        return $"http://{Host}:{Port}";
    }
}
