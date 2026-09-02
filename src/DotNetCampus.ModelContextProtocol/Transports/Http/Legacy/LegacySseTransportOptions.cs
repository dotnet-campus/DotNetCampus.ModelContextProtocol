namespace DotNetCampus.ModelContextProtocol.Transports.Http.Legacy;

/// <summary>
/// 2024-11-05 HTTP+SSE 兼容配置。
/// </summary>
public interface ILegacySseTransportOptions
{
    /// <summary>
    /// 是否启用 2024-11-05 旧协议兼容。
    /// </summary>
    bool IsCompatibleWithSse { get; }

    /// <summary>
    /// 旧协议 SSE 端点。
    /// </summary>
    string? SseEndPoint { get; }

    /// <summary>
    /// 旧协议消息投递端点。
    /// </summary>
    string? SseMessageEndPoint { get; }
}