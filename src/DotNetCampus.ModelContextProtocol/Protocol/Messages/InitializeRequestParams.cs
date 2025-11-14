using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

public sealed record InitializeRequestParams : RequestParams
{
    [JsonPropertyName("protocolVersion")]
    public required string ProtocolVersion { get; init; }

    [JsonPropertyName("capabilities")]
    public required ClientCapabilities Capabilities { get; init; }

    [JsonPropertyName("clientInfo")]
    public required Implementation ClientInfo { get; init; }
}
