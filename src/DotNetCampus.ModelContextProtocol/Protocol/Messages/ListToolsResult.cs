using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

public sealed record ListToolsResult : PaginatedResult
{
    [JsonPropertyName("tools")]
    public IList<Tool> Tools { get; set; } = [];
}
