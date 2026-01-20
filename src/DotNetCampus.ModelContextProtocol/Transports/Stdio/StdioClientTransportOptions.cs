namespace DotNetCampus.ModelContextProtocol.Transports.Stdio;

public record StdioClientTransportOptions
{
    public required string Command { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public IDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>();
}
