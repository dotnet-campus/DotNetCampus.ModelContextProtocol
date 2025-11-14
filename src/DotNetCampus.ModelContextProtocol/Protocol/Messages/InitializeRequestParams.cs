using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

public sealed record InitializeRequestParams : RequestParams
{
    [JsonPropertyName("protocolVersion")]
    public required string ProtocolVersion { get; set; }

    [JsonPropertyName("capabilities")]
    public required ClientCapabilities Capabilities { get; set; }

    [JsonPropertyName("clientInfo")]
    public required Implementation ClientInfo { get; set; }
}
