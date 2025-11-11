using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public class ResourcesCapability
{
    [JsonPropertyName("listChanged")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ListChanged { get; init; }

    [JsonPropertyName("subscribe")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Subscribe { get; init; }
}
