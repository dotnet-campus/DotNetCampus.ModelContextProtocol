namespace DotNetCampus.ModelContextProtocol.Transports.InProcess;

/// <summary>
/// InProcess 传输层配置。<br/>
/// InProcess transport configuration.
/// </summary>
public sealed record InProcessTransportOptions : ITransportOptions
{
    /// <inheritdoc />
    public string Name { get; init; } = "in-process";

    /// <summary>
    /// 消息缓冲区大小<br/>
    /// Message buffer size
    /// </summary>
    public int BufferSize { get; init; } = 100;
}
