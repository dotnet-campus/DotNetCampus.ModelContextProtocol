using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public record PromptsCapability
{
    [JsonPropertyName("listChanged")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ListChanged { get; init; }
}
