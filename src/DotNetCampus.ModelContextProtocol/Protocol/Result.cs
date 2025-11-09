using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public abstract record Result
{
    private protected Result()
    {
    }

    [JsonPropertyName("_meta")]
    public JsonObject? Meta { get; set; }
}