using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// Initialize 响应结果
/// </summary>
public record InitializeResult : Result
{
    [JsonPropertyName("protocolVersion")]
    public required string ProtocolVersion { get; init; }

    [JsonPropertyName("capabilities")]
    public required ServerCapabilities Capabilities { get; init; }

    [JsonPropertyName("serverInfo")]
    public required ServerInfo ServerInfo { get; init; }

    [JsonPropertyName("instructions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required string? Instructions { get; init; }
}
