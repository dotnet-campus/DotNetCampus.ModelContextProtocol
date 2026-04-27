using System.Diagnostics.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Transports.Http;
using DotNetCampus.ModelContextProtocol.Transports.Http.Legacy;

namespace DotNetCampus.ModelContextProtocol.Transports.TouchSocket;

internal interface ITouchSocketHttpServerTransportOptions : ILegacySseTransportOptions
{
    /// <summary>
    /// 指定用于传输的端点。
    /// </summary>
    string EndPoint { get; init; }
}

/// <summary>
/// TouchSocket HTTP 服务端传输层配置选项。
/// </summary>
public record TouchSocketHttpServerTransportOptions : ITouchSocketHttpServerTransportOptions
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

    /// <inheritdoc />
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
    /// 指定是否兼容旧的 SSE 传输层协议（2024-11-05）。默认为 <see langword="true"/>。
    /// </summary>
    /// <remarks>
    /// 2024-11-05 已被服务端明确支持，兼容端点仅为旧客户端额外开放入口，
    /// 不会改变现代客户端使用的 <c>/mcp</c> 行为。这样旧客户端可直接接入；若调用方只希望暴露现代端点，
    /// 可显式设为 <see langword="false"/> 以彰显开发者的底气。
    /// </remarks>
    [MemberNotNullWhen(true, nameof(SseEndPoint), nameof(SseMessageEndPoint))]
    public bool IsCompatibleWithSse { get; init; } = true;

    /// <inheritdoc />
    public string? SseEndPoint => IsCompatibleWithSse ? $"{EndPoint}/sse" : null;

    /// <inheritdoc />
    public string? SseMessageEndPoint => IsCompatibleWithSse ? $"{EndPoint}/messages" : null;
}

/// <summary>
/// 从外部传入的 TouchSocket HTTP 服务端传输层配置选项。
/// </summary>
public record ExternalTouchSocketHttpServerTransportOptions : ITouchSocketHttpServerTransportOptions
{
    /// <inheritdoc />
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
    /// 指定是否兼容旧的 SSE 传输层协议（2024-11-05）。默认为 <see langword="true"/>。
    /// </summary>
    /// <remarks>
    /// 2024-11-05 已被服务端明确支持，兼容端点仅为旧客户端额外开放入口，
    /// 不会改变现代客户端使用的 <c>/mcp</c> 行为。这样旧客户端可直接接入；若调用方只希望暴露现代端点，
    /// 可显式设为 <see langword="false"/> 以彰显开发者的底气。
    /// </remarks>
    [MemberNotNullWhen(true, nameof(SseEndPoint), nameof(SseMessageEndPoint))]
    public bool IsCompatibleWithSse { get; init; } = true;

    /// <inheritdoc />
    public string? SseEndPoint => IsCompatibleWithSse ? $"{EndPoint}/sse" : null;

    /// <inheritdoc />
    public string? SseMessageEndPoint => IsCompatibleWithSse ? $"{EndPoint}/messages" : null;
}
