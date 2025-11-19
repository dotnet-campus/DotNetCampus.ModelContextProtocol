using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 通知取消请求的通知，可由任一方发送以指示它正在取消先前发出的请求。<br/>
/// This notification can be sent by either side to indicate that it is cancelling
/// a previously-issued request.
/// </summary>
public sealed record CancelledNotification : JsonRpcNotification
{
    /// <summary>
    /// 参数<br/>
    /// Parameters
    /// </summary>
    [JsonPropertyName("params")]
    public new required CancelledNotificationParams Params { get; init; }
}

/// <summary>
/// 取消通知的参数<br/>
/// Parameters for cancelled notification
/// </summary>
public sealed record CancelledNotificationParams
{
    /// <summary>
    /// 要取消的请求的 ID。<br/>
    /// 这必须对应于先前在同一方向发出的请求的 ID。<br/>
    /// The ID of the request to cancel.<br/>
    /// This MUST correspond to the ID of a request previously issued in the same direction.
    /// </summary>
    [JsonPropertyName("requestId")]
    public required object RequestId { get; init; }

    /// <summary>
    /// 描述取消原因的可选字符串。可以将其记录或呈现给用户。<br/>
    /// An optional string describing the reason for the cancellation.
    /// This MAY be logged or presented to the user.
    /// </summary>
    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
}

/// <summary>
/// 从客户端发送到服务器的通知，在初始化完成后发送。<br/>
/// This notification is sent from the client to the server after initialization has finished.
/// </summary>
public sealed record InitializedNotification : JsonRpcNotification
{
}

/// <summary>
/// 用于通知接收方长时间运行请求的进度更新的带外通知。<br/>
/// An out-of-band notification used to inform the receiver of a progress update for a long-running request.
/// </summary>
public sealed record ProgressNotification : JsonRpcNotification
{
    /// <summary>
    /// 参数<br/>
    /// Parameters
    /// </summary>
    [JsonPropertyName("params")]
    public new required ProgressNotificationParams Params { get; init; }
}

/// <summary>
/// 进度通知的参数<br/>
/// Parameters for progress notification
/// </summary>
public sealed record ProgressNotificationParams
{
    /// <summary>
    /// 在初始请求中给出的进度令牌，用于将此通知与正在进行的请求关联。<br/>
    /// The progress token which was given in the initial request,
    /// used to associate this notification with the request that is proceeding.
    /// </summary>
    [JsonPropertyName("progressToken")]
    public required object ProgressToken { get; init; }

    /// <summary>
    /// 到目前为止的进度。即使总数未知，每次取得进展时也应增加此值。<br/>
    /// The progress thus far. This should increase every time progress is made,
    /// even if the total is unknown.
    /// </summary>
    [JsonPropertyName("progress")]
    public required double Progress { get; init; }

    /// <summary>
    /// 要处理的项目总数（或所需的总进度），如果已知。<br/>
    /// Total number of items to process (or total progress required), if known.
    /// </summary>
    [JsonPropertyName("total")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Total { get; init; }

    /// <summary>
    /// 描述当前进度的可选消息。<br/>
    /// An optional message describing the current progress.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
}

/// <summary>
/// 从服务器到客户端的可选通知，通知其可以读取的资源列表已更改。<br/>
/// 服务器可以在没有客户端的任何先前订阅的情况下发出此通知。<br/>
/// An optional notification from the server to the client, informing it that the list of resources
/// it can read from has changed. This may be issued by servers without any previous subscription
/// from the client.
/// </summary>
public sealed record ResourceListChangedNotification : JsonRpcNotification
{
}

/// <summary>
/// 从服务器到客户端的通知，通知其资源已更改，可能需要重新读取。<br/>
/// 只有在客户端先前发送了 resources/subscribe 请求时，才应发送此通知。<br/>
/// A notification from the server to the client, informing it that a resource has changed
/// and may need to be read again. This should only be sent if the client previously sent
/// a resources/subscribe request.
/// </summary>
public sealed record ResourceUpdatedNotification : JsonRpcNotification
{
    /// <summary>
    /// 参数<br/>
    /// Parameters
    /// </summary>
    [JsonPropertyName("params")]
    public new required ResourceUpdatedNotificationParams Params { get; init; }
}

/// <summary>
/// 资源更新通知的参数<br/>
/// Parameters for resource updated notification
/// </summary>
public sealed record ResourceUpdatedNotificationParams
{
    /// <summary>
    /// 已更新的资源的 URI。这可能是客户端实际订阅的资源的子资源。<br/>
    /// The URI of the resource that has been updated. This might be a sub-resource
    /// of the one that the client actually subscribed to.
    /// </summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }
}

/// <summary>
/// 从服务器到客户端的可选通知，通知其提供的提示词列表已更改。<br/>
/// 服务器可以在没有客户端的任何先前订阅的情况下发出此通知。<br/>
/// An optional notification from the server to the client, informing it that the list of prompts
/// it offers has changed. This may be issued by servers without any previous subscription from the client.
/// </summary>
public sealed record PromptListChangedNotification : JsonRpcNotification
{
}

/// <summary>
/// 从服务器到客户端的可选通知，通知其提供的工具列表已更改。<br/>
/// 服务器可以在没有客户端的任何先前订阅的情况下发出此通知。<br/>
/// An optional notification from the server to the client, informing it that the list of tools
/// it offers has changed. This may be issued by servers without any previous subscription from the client.
/// </summary>
public sealed record ToolListChangedNotification : JsonRpcNotification
{
}

/// <summary>
/// 从客户端到服务器的通知，通知其根目录列表已更改。<br/>
/// 每当客户端添加、删除或修改任何根目录时，都应发送此通知。<br/>
/// 然后，服务器应使用 ListRootsRequest 请求更新的根目录列表。<br/>
/// A notification from the client to the server, informing it that the list of roots has changed.<br/>
/// This notification should be sent whenever the client adds, removes, or modifies any root.<br/>
/// The server should then request an updated list of roots using the ListRootsRequest.
/// </summary>
public sealed record RootsListChangedNotification : JsonRpcNotification
{
}
