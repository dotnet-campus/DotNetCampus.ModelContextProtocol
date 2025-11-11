using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public class ToolsCapability
{
    [JsonPropertyName("listChanged")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ListChanged { get; init; }
}
