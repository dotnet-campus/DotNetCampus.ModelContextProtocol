using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

#region Task Metadata and Status

/// <summary>
/// 任务元数据，用于在请求中标识和描述任务。<br/>
/// Task metadata for identifying and describing tasks in requests.
/// </summary>
public sealed record TaskMetadata
{
    /// <summary>
    /// 任务的唯一标识符。<br/>
    /// The unique identifier for the task.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// 可选的任务标题，用于显示。<br/>
    /// Optional task title for display purposes.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }
}

/// <summary>
/// 关联任务的元数据，包含状态信息。<br/>
/// Metadata for related tasks, including status information.
/// </summary>
public sealed record RelatedTaskMetadata
{
    /// <summary>
    /// 任务的唯一标识符。<br/>
    /// The unique identifier for the task.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// 可选的任务标题。<br/>
    /// Optional task title.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// 任务的当前状态。<br/>
    /// The current status of the task.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; init; }
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
/// Complete task information.
/// </summary>
public sealed record Task
{
    /// <summary>
    /// 任务的唯一标识符。<br/>
    /// The unique identifier for the task.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// 任务的当前状态。<br/>
    /// The current status of the task.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// 可选的任务标题。<br/>
    /// Optional task title.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// 任务的创建时间（ISO 8601 格式）。<br/>
    /// The task creation time in ISO 8601 format.
    /// </summary>
    [JsonPropertyName("created")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Created { get; init; }

    /// <summary>
    /// 任务的最后更新时间（ISO 8601 格式）。<br/>
    /// The task last update time in ISO 8601 format.
    /// </summary>
    [JsonPropertyName("updated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Updated { get; init; }

    /// <summary>
    /// 任务的进度信息（0-100 的整数）。<br/>
    /// Task progress information (integer from 0-100).
    /// </summary>
    [JsonPropertyName("progress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Progress { get; init; }

    /// <summary>
    /// 任务的附加元数据。<br/>
    /// Additional metadata for the task.
    /// </summary>
    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Metadata { get; init; }
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
    /// 任务的唯一标识符。<br/>
    /// The unique identifier for the task.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}

/// <summary>
/// 获取任务执行结果的请求参数。<br/>
/// Request parameters for getting task execution result.
/// </summary>
public sealed record GetTaskPayloadRequestParams : RequestParams
{
    /// <summary>
    /// 任务的唯一标识符。<br/>
    /// The unique identifier for the task.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}

/// <summary>
/// 取消任务的请求参数。<br/>
/// Request parameters for cancelling a task.
/// </summary>
public sealed record CancelTaskRequestParams : RequestParams
{
    /// <summary>
    /// 任务的唯一标识符。<br/>
    /// The unique identifier for the task.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// 可选的取消原因。<br/>
    /// Optional reason for cancellation.
    /// </summary>
    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
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
/// 任务增强请求的响应结果，包含任务标识。<br/>
/// Response result for task-augmented requests, containing task identification.
/// </summary>
public sealed record CreateTaskResult : Result
{
    /// <summary>
    /// 创建的任务标识符。<br/>
    /// The identifier of the created task.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }
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
    public required Task Task { get; init; }
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
    public required IReadOnlyList<Task> Tasks { get; init; }
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
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// 任务的新状态。<br/>
    /// The new status of the task.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// 可选的进度信息（0-100）。<br/>
    /// Optional progress information (0-100).
    /// </summary>
    [JsonPropertyName("progress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Progress { get; init; }

    /// <summary>
    /// 可选的状态消息。<br/>
    /// Optional status message.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
}

#endregion
