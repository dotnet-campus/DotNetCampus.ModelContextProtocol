using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

public record ReadResourceResult : Result
{
    [JsonPropertyName("contents")]
    public IList<ResourceContents> Contents { get; set; } = [];
}
