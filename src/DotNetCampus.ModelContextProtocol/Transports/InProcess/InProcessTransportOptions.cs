namespace DotNetCampus.ModelContextProtocol.Transports.InProcess;

/// <summary>
/// In-Process 传输层选项。
/// </summary>
public sealed record InProcessTransportOptions
{
    /// <summary>
    /// 获取队列容量。为 <see langword="null"/> 时使用无界队列；为正整数时使用有界队列。
    /// </summary>
    public int? Capacity { get; init; }
}