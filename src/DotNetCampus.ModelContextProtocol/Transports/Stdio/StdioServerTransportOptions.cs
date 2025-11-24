namespace DotNetCampus.ModelContextProtocol.Transports.Stdio;

/// <summary>
/// stdio 传输层配置。<br/>
/// stdio transport configuration.
/// </summary>
public sealed record StdioServerTransportOptions : ITransportOptions
{
    /// <inheritdoc />
    public string Name { get; init; } = "stdio";
}
