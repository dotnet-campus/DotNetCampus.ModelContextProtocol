using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// 服务端信息
/// </summary>
public class ServerInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }
}
