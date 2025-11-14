using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// MCP 请求参数基类
/// </summary>
public abstract record RequestParams
{
    private protected RequestParams()
    {
    }

    /// <summary>
    /// 元数据字段
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? Meta { get; set; }
}

/// <summary>
/// MCP 响应结果基类
/// </summary>
public abstract record Result
{
    private protected Result()
    {
    }

    /// <summary>
    /// 元数据字段
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? Meta { get; init; }
}

/// <summary>
/// MCP 分页请求参数基类
/// </summary>
public abstract record PaginatedRequestParams : RequestParams
{
    private protected PaginatedRequestParams()
    {
    }

    /// <summary>
    /// 用于分页的游标
    /// </summary>
    [JsonPropertyName("cursor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cursor { get; init; }
}

/// <summary>
/// 支持分页的响应结果基类
/// </summary>
public abstract record PaginatedResult : Result
{
    private protected PaginatedResult()
    {
    }

    /// <summary>
    /// 下一页的游标
    /// </summary>
    [JsonPropertyName("nextCursor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextCursor { get; init; }
}
