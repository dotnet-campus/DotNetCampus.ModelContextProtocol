using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Protocol;

namespace DotNetCampus.ModelContextProtocol.Messages;

public record ClientCapabilities
{
    [JsonPropertyName("roots")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RootsCapability? Roots { get; set; }

    [JsonPropertyName("sampling")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Sampling { get; set; }

    [JsonPropertyName("elicitation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Elicitation { get; set; }

    [JsonPropertyName("experimental")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Experimental { get; set; }
}
