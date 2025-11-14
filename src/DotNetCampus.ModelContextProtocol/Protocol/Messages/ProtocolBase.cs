using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// MCP 请求参数基类<br/>
/// Base class for MCP request parameters
/// </summary>
public abstract record RequestParams
{
    private protected RequestParams()
    {
    }

    /// <summary>
    /// 元数据字段<br/>
    /// See <a href="https://modelcontextprotocol.io/specification/2025-06-18/basic/index#meta">
    /// General fields: _meta</a> for notes on _meta usage.
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? Meta { get; set; }
}

/// <summary>
/// MCP 响应结果基类<br/>
/// Base class for MCP response results
/// </summary>
public abstract record Result
{
    private protected Result()
    {
    }

    /// <summary>
    /// 元数据字段<br/>
    /// See <a href="https://modelcontextprotocol.io/specification/2025-06-18/basic/index#meta">
    /// General fields: _meta</a> for notes on _meta usage.
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? Meta { get; init; }
}

/// <summary>
/// MCP 分页请求参数基类<br/>
/// Base class for paginated MCP request parameters
/// </summary>
public abstract record PaginatedRequestParams : RequestParams
{
    private protected PaginatedRequestParams()
    {
    }

    /// <summary>
    /// 用于分页的游标<br/>
    /// An opaque token representing the current pagination position.
    /// If provided, the server should return results starting after this cursor.
    /// </summary>
    [JsonPropertyName("cursor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cursor { get; init; }
}

/// <summary>
/// 支持分页的响应结果基类<br/>
/// Base class for paginated response results
/// </summary>
public abstract record PaginatedResult : Result
{
    private protected PaginatedResult()
    {
    }

    /// <summary>
    /// 下一页的游标<br/>
    /// An opaque token representing the pagination position after the last returned result.
    /// If present, there may be more results available.
    /// </summary>
    [JsonPropertyName("nextCursor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextCursor { get; init; }
}
