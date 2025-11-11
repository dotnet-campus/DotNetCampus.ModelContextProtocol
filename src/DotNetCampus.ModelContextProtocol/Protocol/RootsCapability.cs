using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public record RootsCapability
{
    [JsonPropertyName("listChanged")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required bool? ListChanged { get; init; }
}
