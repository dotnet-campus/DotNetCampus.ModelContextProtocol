using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 服务端可能支持的能力。<br/>
/// 已知的能力在此架构中定义，但这不是一个封闭集：
/// 任何服务端都可以定义自己的额外能力。<br/>
/// Capabilities that a server may support.
/// Known capabilities are defined here, in this schema,
/// but this is not a closed set: any server can define its own, additional capabilities.
/// </summary>
public record ServerCapabilities
{
    /// <summary>
    /// 如果存在，表示服务器提供可读取的资源。<br/>
    /// Present if the server offers any resources to read.
    /// </summary>
    [JsonPropertyName("resources")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required ResourcesCapability? Resources { get; init; }

    /// <summary>
    /// 如果存在，表示服务器提供任何提示词模板。<br/>
    /// Present if the server offers any prompt templates.
    /// </summary>
    [JsonPropertyName("prompts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required PromptsCapability? Prompts { get; init; }

    /// <summary>
    /// 如果存在，表示服务器提供可调用的工具。<br/>
    /// Present if the server offers any tools to call.
    /// </summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required ToolsCapability? Tools { get; init; }

    /// <summary>
    /// 如果存在，表示服务器支持向客户端发送日志消息。<br/>
    /// Present if the server supports sending log messages to the client.
    /// </summary>
    [JsonPropertyName("logging")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Logging { get; init; }

    /// <summary>
    /// 如果存在，表示服务器支持参数自动完成建议。<br/>
    /// Present if the server supports argument autocompletion suggestions.
    /// </summary>
    [JsonPropertyName("completions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Completions { get; init; }

    /// <summary>
    /// 如果存在，表示服务器支持任务系统。<br/>
    /// Present if the server supports task system.
    /// </summary>
    [JsonPropertyName("tasks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TasksServerCapability? Tasks { get; init; }

    /// <summary>
    /// 服务端支持的实验性、非标准能力。<br/>
    /// Experimental, non-standard capabilities that the server supports.
    /// </summary>
    [JsonPropertyName("experimental")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Experimental { get; init; }
}

/// <summary>
/// 提示词能力<br/>
/// Prompts capability
/// </summary>
public record PromptsCapability
{
    /// <summary>
    /// 此服务器是否支持提示词列表更改的通知。<br/>
    /// Whether this server supports notifications for changes to the prompt list.
    /// </summary>
    [JsonPropertyName("listChanged")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ListChanged { get; init; }
}

/// <summary>
/// 资源能力<br/>
/// Resources capability
/// </summary>
public record ResourcesCapability
{
    /// <summary>
    /// 此服务器是否支持资源列表更改的通知。<br/>
    /// Whether this server supports notifications for changes to the resource list.
    /// </summary>
    [JsonPropertyName("listChanged")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ListChanged { get; init; }

    /// <summary>
    /// 此服务器是否支持订阅资源更新。<br/>
    /// Whether this server supports subscribing to resource updates.
    /// </summary>
    [JsonPropertyName("subscribe")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Subscribe { get; init; }
}

/// <summary>
/// 根目录能力<br/>
/// Roots capability
/// </summary>
public record RootsCapability
{
    /// <summary>
    /// 客户端是否支持根目录列表更改的通知。<br/>
    /// Whether the client supports notifications for changes to the roots list.
    /// </summary>
    [JsonPropertyName("listChanged")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required bool? ListChanged { get; init; }
}

/// <summary>
/// 工具能力<br/>
/// Tools capability
/// </summary>
public record ToolsCapability
{
    /// <summary>
    /// 此服务器是否支持工具列表更改的通知。<br/>
    /// Whether this server supports notifications for changes to the tool list.
    /// </summary>
    [JsonPropertyName("listChanged")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ListChanged { get; init; }
}
