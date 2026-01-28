using System.Diagnostics.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// HTTP 传输层配置选项。
/// </summary>
public record LocalHostHttpServerTransportOptions
{
    /// <summary>
    /// 指定用于传输的端口号。
    /// </summary>
    public required int Port { get; init; }

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
    /// 按照 MCP 官方协议规范对传输层的要求：<br/>
    /// 服务器必须验证所有传入连接的 Origin 标头，以防止 DNS 重绑定攻击。<br/>
    /// Servers MUST validate the Origin header on all incoming connections to prevent DNS rebinding attacks<br/>
    /// 当启用时，会验证 Host 和 Origin header，拒绝非本机来源的请求。<br/>
    /// 默认值：true（推荐）。
    /// </summary>
    public bool EnableDnsRebindingProtection { get; init; } = true;

    /// <summary>
    /// 指定是否兼容旧的 SSE 传输层协议（2024-11-05）。默认为 <see langword="false"/>。
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

    /// <summary>
    /// 指定用于传输的基础 URL。
    /// </summary>
    public IReadOnlyList<string> GetUrlPrefixes()
    {
        return [$"http://localhost:{Port}/", $"http://127.0.0.1:{Port}/", $"http://[::1]:{Port}/"];
    }
}
