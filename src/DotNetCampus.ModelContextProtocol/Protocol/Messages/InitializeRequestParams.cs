using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public sealed record InitializeRequestParams : RequestParams
{
    [JsonPropertyName("protocolVersion")]
    public required string ProtocolVersion { get; set; }

    [JsonPropertyName("capabilities")]
    public required ClientCapabilities Capabilities { get; set; }

    [JsonPropertyName("clientInfo")]
    public required Implementation ClientInfo { get; set; }
}

public sealed record PingRequestParams : RequestParams
{
}
