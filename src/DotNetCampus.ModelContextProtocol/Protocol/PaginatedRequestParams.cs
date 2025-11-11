using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public abstract record PaginatedRequestParams : RequestParams
{
    private protected PaginatedRequestParams()
    {
    }

    [JsonPropertyName("cursor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cursor { get; set; }
}
