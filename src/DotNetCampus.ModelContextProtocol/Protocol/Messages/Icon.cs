using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public sealed record Icon
{
    [JsonPropertyName("src")]
    public required string Source { get; set; }

    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; set; }

    [JsonPropertyName("sizes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<string>? Sizes { get; set; }

    [JsonPropertyName("theme")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Theme { get; set; }
}
