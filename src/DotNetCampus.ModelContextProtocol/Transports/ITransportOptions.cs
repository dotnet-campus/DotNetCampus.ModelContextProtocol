namespace DotNetCampus.ModelContextProtocol.Transports;

public interface ITransportOptions
{
    /// <summary>
    /// 传输层名称（可选，用于日志）<br/>
    /// Transport name (optional, for logging)
    /// </summary>
    string Name { get; init; }
}
