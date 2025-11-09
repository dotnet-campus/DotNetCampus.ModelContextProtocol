using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public abstract record RequestParams
{
    private protected RequestParams()
    {
    }

    [JsonPropertyName("_meta")]
    public JsonObject? Meta { get; set; }
}
