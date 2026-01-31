using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 服务器能够读取的已知资源。<br/>
/// A known resource that the server is capable of reading.
/// </summary>
public sealed record Resource : IBaseMetadata
{
    /// <summary>
    /// 用于编程或逻辑使用，但在过去的规范中或作为后备用于显示名称（如果 title 不存在）。<br/>
    /// Intended for programmatic or logical use, but used as a display name in past specs
    /// or fallback (if title isn't present).
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 用于 UI 和最终用户上下文 — 优化为可读并易于理解，即使对不熟悉特定领域术语的人也是如此。<br/>
    /// 如果未提供，应使用 name 作为显示名称。<br/>
    /// Intended for UI and end-user contexts — optimized to be human-readable
    /// and easily understood, even by those unfamiliar with domain-specific terminology.<br/>
    /// If not provided, the name should be used for display.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// 此资源的 URI。<br/>
    /// The URI of this resource.
    /// </summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    /// <summary>
    /// 此资源表示什么的描述。<br/>
    /// 客户端可以使用这些信息来改善 LLM 对可用资源的理解。<br/>
    /// 可以将其视为给模型的"提示"。<br/>
    /// A description of what this resource represents.<br/>
    /// This can be used by clients to improve the LLM's understanding of available resources.
    /// It can be thought of like a "hint" to the model.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// 图标列表<br/>
    /// List of icons
    /// </summary>
    [JsonPropertyName("icons")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<Icon>? Icons { get; init; }

    /// <summary>
    /// 此资源的 MIME 类型（如果已知）。<br/>
    /// The MIME type of this resource, if known.
    /// </summary>
    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; init; }

    /// <summary>
    /// 可选的客户端注解。<br/>
    /// Optional annotations for the client.
    /// </summary>
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Annotations? Annotations { get; init; }

    /// <summary>
    /// 原始资源内容的大小（以字节为单位）（即，在 base64 编码或任何分词之前），如果已知。<br/>
    /// 客户端可以使用它来显示文件大小并估计上下文窗口使用情况。<br/>
    /// The size of the raw resource content, in bytes (i.e., before base64 encoding or any tokenization),
    /// if known.<br/>
    /// This can be used by Hosts to display file sizes and estimate context window usage.
    /// </summary>
    [JsonPropertyName("size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Size { get; init; }

    /// <summary>
    /// 元数据字段<br/>
    /// See <a href="https://modelcontextprotocol.io/specification/2025-11-25/basic/index#meta">
    /// General fields: _meta</a> for notes on _meta usage.
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Meta { get; init; }
}

/// <summary>
/// 服务器上可用资源的模板描述。<br/>
/// A template description for resources available on the server.
/// </summary>
public sealed record ResourceTemplate : IBaseMetadata
{
    /// <summary>
    /// 用于编程或逻辑使用，但在过去的规范中或作为后备用于显示名称（如果 title 不存在）。<br/>
    /// Intended for programmatic or logical use, but used as a display name in past specs
    /// or fallback (if title isn't present).
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 用于 UI 和最终用户上下文 — 优化为可读并易于理解，即使对不熟悉特定领域术语的人也是如此。<br/>
    /// 如果未提供，应使用 name 作为显示名称。<br/>
    /// Intended for UI and end-user contexts — optimized to be human-readable
    /// and easily understood, even by those unfamiliar with domain-specific terminology.<br/>
    /// If not provided, the name should be used for display.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// 可用于构造资源 URI 的 URI 模板（根据 RFC 6570）。<br/>
    /// A URI template (according to RFC 6570) that can be used to construct resource URIs.
    /// </summary>
    [JsonPropertyName("uriTemplate")]
    public required string UriTemplate { get; init; }

    /// <summary>
    /// 此模板用途的描述。<br/>
    /// 客户端可以使用这些信息来改善 LLM 对可用资源的理解。<br/>
    /// 可以将其视为给模型的"提示"。<br/>
    /// A description of what this template is for.<br/>
    /// This can be used by clients to improve the LLM's understanding of available resources.
    /// It can be thought of like a "hint" to the model.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// 匹配此模板的所有资源的 MIME 类型。<br/>
    /// 只有在匹配此模板的所有资源具有相同类型时，才应包含此项。<br/>
    /// The MIME type for all resources that match this template.
    /// This should only be included if all resources matching this template have the same type.
    /// </summary>
    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; init; }

    /// <summary>
    /// 可选的客户端注解。<br/>
    /// Optional annotations for the client.
    /// </summary>
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Annotations? Annotations { get; init; }

    /// <summary>
    /// 元数据字段<br/>
    /// See <a href="https://modelcontextprotocol.io/specification/2025-11-25/basic/index#meta">
    /// General fields: _meta</a> for notes on _meta usage.
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Meta { get; init; }
}

/// <summary>
/// 从客户端发送以请求服务器拥有的资源列表。<br/>
/// Sent from the client to request a list of resources the server has.
/// </summary>
public sealed record ListResourcesRequestParams : PaginatedRequestParams
{
}

/// <summary>
/// 服务器对客户端的 resources/list 请求的响应。<br/>
/// The server's response to a resources/list request from the client.
/// </summary>
public sealed record ListResourcesResult : PaginatedResult
{
    /// <summary>
    /// 资源列表<br/>
    /// List of resources
    /// </summary>
    [JsonPropertyName("resources")]
    public required IReadOnlyList<Resource> Resources { get; init; }
}

/// <summary>
/// 从客户端发送以请求服务器拥有的资源模板列表。<br/>
/// Sent from the client to request a list of resource templates the server has.
/// </summary>
public sealed record ListResourceTemplatesRequestParams : PaginatedRequestParams
{
}

/// <summary>
/// 服务器对客户端的 resources/templates/list 请求的响应。<br/>
/// The server's response to a resources/templates/list request from the client.
/// </summary>
public sealed record ListResourceTemplatesResult : PaginatedResult
{
    /// <summary>
    /// 资源模板列表<br/>
    /// List of resource templates
    /// </summary>
    [JsonPropertyName("resourceTemplates")]
    public required IReadOnlyList<ResourceTemplate> ResourceTemplates { get; init; }
}

/// <summary>
/// 从客户端发送以请求在特定资源更改时从服务器接收 resources/updated 通知。<br/>
/// Sent from the client to request resources/updated notifications from the server
/// whenever a particular resource changes.
/// </summary>
public sealed record SubscribeRequestParams : RequestParams
{
    /// <summary>
    /// 要订阅的资源的 URI。URI 可以使用任何协议；由服务器决定如何解释它。<br/>
    /// The URI of the resource to subscribe to. The URI can use any protocol;
    /// it is up to the server how to interpret it.
    /// </summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }
}

/// <summary>
/// 从客户端发送以请求取消来自服务器的 resources/updated 通知。<br/>
/// 这应该遵循先前的 resources/subscribe 请求。<br/>
/// Sent from the client to request cancellation of resources/updated notifications from the server.
/// This should follow a previous resources/subscribe request.
/// </summary>
public sealed record UnsubscribeRequestParams : RequestParams
{
    /// <summary>
    /// 要取消订阅的资源的 URI。<br/>
    /// The URI of the resource to unsubscribe from.
    /// </summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }
}
