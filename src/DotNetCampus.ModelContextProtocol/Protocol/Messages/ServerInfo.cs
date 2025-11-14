using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 服务端信息<br/>
/// Server information
/// </summary>
public record ServerInfo
{
    /// <summary>
    /// 服务器名称<br/>
    /// Server name
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 服务器版本<br/>
    /// Server version
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }
}
