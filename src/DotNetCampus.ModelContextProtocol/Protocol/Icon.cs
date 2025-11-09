using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public sealed class Icon
{
    [JsonPropertyName("src")]
    public required string Source { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("sizes")]
    public IList<string>? Sizes { get; set; }

    [JsonPropertyName("theme")]
    public string? Theme { get; set; }
}
