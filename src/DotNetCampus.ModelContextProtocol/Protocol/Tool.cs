using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public sealed record Tool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; set; }

    [JsonPropertyName("outputSchema")]
    public JsonElement? OutputSchema { get; set; }

    [JsonPropertyName("annotations")]
    public ToolAnnotations? Annotations { get; set; }

    [JsonPropertyName("icons")]
    public IList<Icon>? Icons { get; set; }

    [JsonPropertyName("_meta")]
    public JsonObject? Meta { get; set; }
}