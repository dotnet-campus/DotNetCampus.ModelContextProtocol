using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public sealed class ToolAnnotations
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("destructiveHint")]
    public bool? DestructiveHint { get; set; }

    [JsonPropertyName("idempotentHint")]
    public bool? IdempotentHint { get; set; }

    [JsonPropertyName("openWorldHint")]
    public bool? OpenWorldHint { get; set; }

    [JsonPropertyName("readOnlyHint")]
    public bool? ReadOnlyHint { get; set; }
}
