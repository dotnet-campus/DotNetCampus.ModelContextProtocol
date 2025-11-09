using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public sealed record ClientInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }

    [JsonPropertyName("icons")]
    public IList<Icon>? Icons { get; set; }

    [JsonPropertyName("websiteUrl")]
    public string? WebsiteUrl { get; set; }
}
