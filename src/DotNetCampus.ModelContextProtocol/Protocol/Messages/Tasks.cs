using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

#region Task Metadata and Status

/// <summary>
/// 任务元数据，用于在请求中增强任务执行。<br/>
/// Metadata for augmenting a request with task execution.
/// Include this in the `task` field of the request parameters.
/// </summary>
public sealed record TaskMetadata
{
    /// <summary>
    /// 请求从创建时起保留任务的时长（毫秒）。<br/>
    /// Requested duration in milliseconds to retain task from creation.
    /// </summary>
    [JsonPropertyName("ttl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Ttl { get; init; }
}

/// <summary>
/// 关联任务的元数据。<br/>
/// 包含在 `_meta` 字段中，键名为 `io.modelcontextprotocol/related-task`。<br/>
/// Metadata for associating messages with a task.<br/>
/// Include this in the `_meta` field under the key `io.modelcontextprotocol/related-task`.
/// </summary>
public sealed record RelatedTaskMetadata
{
    /// <summary>
    /// 此消息关联的任务标识符。<br/>
    /// The task identifier this message is associated with.
    /// </summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; init; }
}

/// <summary>
/// 任务状态常量定义。<br/>
/// Task status constant definitions.
/// </summary>
public static class TaskStatus
{
    /// <summary>
    /// 任务正在进行中。<br/>
    /// The task is currently in progress.
    /// </summary>
    public const string Working = "working";

    /// <summary>
    /// 任务需要用户输入才能继续。<br/>
    /// The task requires user input to proceed.
    /// </summary>
    public const string InputRequired = "input_required";

    /// <summary>
    /// 任务已成功完成。<br/>
    /// The task has been completed successfully.
    /// </summary>
    public const string Completed = "completed";

    /// <summary>
    /// 任务执行失败。<br/>
    /// The task has failed.
    /// </summary>
    public const string Failed = "failed";

    /// <summary>
    /// 任务已被取消。<br/>
    /// The task has been cancelled.
    /// </summary>
    public const string Cancelled = "cancelled";
}

#endregion

#region Task Data

/// <summary>
/// 任务完整信息。<br/>
/// Data associated with a task.
/// </summary>
public sealed record McpTask
{
    /// <summary>
    /// 任务标识符。<br/>
    /// The task identifier.
    /// </summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; init; }

    /// <summary>
    /// 当前任务状态。<br/>
    /// Current task state.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// 可选的人类可读消息，描述当前任务状态。<br/>
    /// 这可以为任何状态提供上下文，包括：<br/>
    /// - "cancelled" 状态的原因<br/>
    /// - "completed" 状态的摘要<br/>
    /// - "failed" 状态的诊断信息（例如，错误详情、出了什么问题）<br/>
    /// Optional human-readable message describing the current task state.<br/>
    /// This can provide context for any status, including:<br/>
    /// - Reasons for "cancelled" status<br/>
    /// - Summaries for "completed" status<br/>
    /// - Diagnostic information for "failed" status (e.g., error details, what went wrong)
    /// </summary>
    [JsonPropertyName("statusMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusMessage { get; init; }

    /// <summary>
    /// 任务创建时的 ISO 8601 时间戳。<br/>
    /// ISO 8601 timestamp when the task was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public required string CreatedAt { get; init; }

    /// <summary>
    /// 任务最后更新时的 ISO 8601 时间戳。<br/>
    /// ISO 8601 timestamp when the task was last updated.
    /// </summary>
    [JsonPropertyName("lastUpdatedAt")]
    public required string LastUpdatedAt { get; init; }

    /// <summary>
    /// 从创建时起的实际保留时长（毫秒），null 表示无限期。<br/>
    /// Actual retention duration from creation in milliseconds, null for unlimited.
    /// </summary>
    [JsonPropertyName("ttl")]
    public required int? Ttl { get; init; }

    /// <summary>
    /// 建议的轮询间隔（毫秒）。<br/>
    /// Suggested polling interval in milliseconds.
    /// </summary>
    [JsonPropertyName("pollInterval")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PollInterval { get; init; }
}

#endregion

#region Task Capabilities

/// <summary>
/// 客户端的任务能力声明。<br/>
/// Client task capability declaration.
/// </summary>
public sealed record TasksClientCapability
{
    /// <summary>
    /// 是否支持列出任务。<br/>
    /// Whether listing tasks is supported.
    /// </summary>
    [JsonPropertyName("list")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? List { get; init; }

    /// <summary>
    /// 是否支持取消任务。<br/>
    /// Whether cancelling tasks is supported.
    /// </summary>
    [JsonPropertyName("cancel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Cancel { get; init; }

    /// <summary>
    /// 支持任务增强的请求类型。<br/>
    /// Request types that support task augmentation.
    /// </summary>
    [JsonPropertyName("requests")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TasksClientRequestsCapability? Requests { get; init; }
}

/// <summary>
/// 客户端支持任务增强的请求类型。<br/>
/// Client request types that support task augmentation.
/// </summary>
public sealed record TasksClientRequestsCapability
{
    /// <summary>
    /// 采样相关的任务支持。<br/>
    /// Task support for sampling-related requests.
    /// </summary>
    [JsonPropertyName("sampling")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TasksClientSamplingCapability? Sampling { get; init; }

    /// <summary>
    /// 引出相关的任务支持。<br/>
    /// Task support for elicitation-related requests.
    /// </summary>
    [JsonPropertyName("elicitation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TasksClientElicitationCapability? Elicitation { get; init; }
}

/// <summary>
/// 客户端采样请求的任务支持。<br/>
/// Client task support for sampling requests.
/// </summary>
public sealed record TasksClientSamplingCapability
{
    /// <summary>
    /// 是否支持 createMessage 请求的任务增强。<br/>
    /// Whether task augmentation is supported for createMessage requests.
    /// </summary>
    [JsonPropertyName("createMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? CreateMessage { get; init; }
}

/// <summary>
/// 客户端引出请求的任务支持。<br/>
/// Client task support for elicitation requests.
/// </summary>
public sealed record TasksClientElicitationCapability
{
    /// <summary>
    /// 是否支持 create 请求的任务增强。<br/>
    /// Whether task augmentation is supported for create requests.
    /// </summary>
    [JsonPropertyName("create")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Create { get; init; }
}

/// <summary>
/// 服务器的任务能力声明。<br/>
/// Server task capability declaration.
/// </summary>
public sealed record TasksServerCapability
{
    /// <summary>
    /// 是否支持列出任务。<br/>
    /// Whether listing tasks is supported.
    /// </summary>
    [JsonPropertyName("list")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? List { get; init; }

    /// <summary>
    /// 是否支持取消任务。<br/>
    /// Whether cancelling tasks is supported.
    /// </summary>
    [JsonPropertyName("cancel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Cancel { get; init; }

    /// <summary>
    /// 支持任务增强的请求类型。<br/>
    /// Request types that support task augmentation.
    /// </summary>
    [JsonPropertyName("requests")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TasksServerRequestsCapability? Requests { get; init; }
}

/// <summary>
/// 服务器支持任务增强的请求类型。<br/>
/// Server request types that support task augmentation.
/// </summary>
public sealed record TasksServerRequestsCapability
{
    /// <summary>
    /// 工具调用相关的任务支持。<br/>
    /// Task support for tool call requests.
    /// </summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TasksServerToolsCapability? Tools { get; init; }
}

/// <summary>
/// 服务器工具调用的任务支持。<br/>
/// Server task support for tool calls.
/// </summary>
public sealed record TasksServerToolsCapability
{
    /// <summary>
    /// 是否支持 call 请求的任务增强。<br/>
    /// Whether task augmentation is supported for call requests.
    /// </summary>
    [JsonPropertyName("call")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Call { get; init; }
}

#endregion

#region Task Request Parameters

/// <summary>
/// 获取任务详情的请求参数。<br/>
/// Request parameters for getting task details.
/// </summary>
public sealed record GetTaskRequestParams : RequestParams
{
    /// <summary>
    /// 要查询的任务标识符。<br/>
    /// The task identifier to query.
    /// </summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; init; }
}

/// <summary>
/// 获取任务执行结果的请求参数。<br/>
/// Request parameters for getting task execution result.
/// </summary>
public sealed record GetTaskPayloadRequestParams : RequestParams
{
    /// <summary>
    /// 要检索结果的任务标识符。<br/>
    /// The task identifier to retrieve results for.
    /// </summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; init; }
}

/// <summary>
/// 取消任务的请求参数。<br/>
/// Request parameters for cancelling a task.
/// </summary>
public sealed record CancelTaskRequestParams : RequestParams
{
    /// <summary>
    /// 要取消的任务标识符。<br/>
    /// The task identifier to cancel.
    /// </summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; init; }
}

/// <summary>
/// 列出任务的请求参数。<br/>
/// Request parameters for listing tasks.
/// </summary>
public sealed record ListTasksRequestParams : PaginatedRequestParams
{
}

#endregion

#region Task Results

/// <summary>
/// 任务增强请求的响应结果。<br/>
/// A response to a task-augmented request.
/// </summary>
public sealed record CreateTaskResult : Result
{
    /// <summary>
    /// 创建的任务信息。<br/>
    /// The created task information.
    /// </summary>
    [JsonPropertyName("task")]
    public required McpTask Task { get; init; }
}

/// <summary>
/// 获取任务详情的响应结果。<br/>
/// Response result for getting task details.
/// </summary>
public sealed record GetTaskResult : Result
{
    /// <summary>
    /// 任务信息。<br/>
    /// The task information.
    /// </summary>
    [JsonPropertyName("task")]
    public required McpTask Task { get; init; }
}

/// <summary>
/// 获取任务执行结果的响应。<br/>
/// Response for getting task execution result.
/// </summary>
public sealed record GetTaskPayloadResult : Result
{
    /// <summary>
    /// 任务的执行结果负载。<br/>
    /// The execution result payload of the task.
    /// </summary>
    [JsonPropertyName("payload")]
    public required JsonElement Payload { get; init; }
}

/// <summary>
/// 取消任务的响应结果。<br/>
/// Response result for cancelling a task.
/// </summary>
public sealed record CancelTaskResult : Result
{
}

/// <summary>
/// 列出任务的响应结果。<br/>
/// Response result for listing tasks.
/// </summary>
public sealed record ListTasksResult : PaginatedResult
{
    /// <summary>
    /// 任务列表。<br/>
    /// The list of tasks.
    /// </summary>
    [JsonPropertyName("tasks")]
    public required IReadOnlyList<McpTask> Tasks { get; init; }
}

#endregion

#region Task Notifications

/// <summary>
/// 任务状态更新通知。<br/>
/// Task status update notification.
/// </summary>
public sealed record TaskStatusNotification : JsonRpcNotification
{
    /// <summary>
    /// 通知参数。<br/>
    /// Notification parameters.
    /// </summary>
    [JsonPropertyName("params")]
    public new required TaskStatusNotificationParams Params { get; init; }
}

/// <summary>
/// 任务状态更新通知的参数。<br/>
/// Parameters for task status update notification.
/// </summary>
public sealed record TaskStatusNotificationParams
{
    /// <summary>
    /// 任务标识符。<br/>
    /// The task identifier.
    /// </summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; init; }

    /// <summary>
    /// 当前任务状态。<br/>
    /// Current task state.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// 可选的人类可读消息，描述当前任务状态。<br/>
    /// Optional human-readable message describing the current task state.
    /// </summary>
    [JsonPropertyName("statusMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusMessage { get; init; }

    /// <summary>
    /// 任务创建时的 ISO 8601 时间戳。<br/>
    /// ISO 8601 timestamp when the task was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public required string CreatedAt { get; init; }

    /// <summary>
    /// 任务最后更新时的 ISO 8601 时间戳。<br/>
    /// ISO 8601 timestamp when the task was last updated.
    /// </summary>
    [JsonPropertyName("lastUpdatedAt")]
    public required string LastUpdatedAt { get; init; }

    /// <summary>
    /// 从创建时起的实际保留时长（毫秒），null 表示无限期。<br/>
    /// Actual retention duration from creation in milliseconds, null for unlimited.
    /// </summary>
    [JsonPropertyName("ttl")]
    public required int? Ttl { get; init; }

    /// <summary>
    /// 建议的轮询间隔（毫秒）。<br/>
    /// Suggested polling interval in milliseconds.
    /// </summary>
    [JsonPropertyName("pollInterval")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PollInterval { get; init; }
}

#endregion
