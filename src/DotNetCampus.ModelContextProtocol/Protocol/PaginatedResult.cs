using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public abstract record PaginatedResult : Result
{
    private protected PaginatedResult()
    {
    }

    [JsonPropertyName("nextCursor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextCursor { get; set; }
}