namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// 提供 MCP 协议中使用的常见请求方法名称的常量。<br/>
/// Provides constants with the names of common request methods used in the MCP protocol.
/// </summary>
public static class RequestMethods
{
    /// <summary>
    /// 从客户端发送以请求服务器工具列表的请求方法名称。<br/>
    /// The name of the request method sent from the client to request a list of the server's tools.
    /// </summary>
    public const string ToolsList = "tools/list";

    /// <summary>
    /// 从客户端发送以请求服务器调用特定工具的请求方法名称。<br/>
    /// The name of the request method sent from the client to request that the server invoke a specific tool.
    /// </summary>
    public const string ToolsCall = "tools/call";

    /// <summary>
    /// 从客户端发送以请求服务器提示词列表的请求方法名称。<br/>
    /// The name of the request method sent from the client to request a list of the server's prompts.
    /// </summary>
    public const string PromptsList = "prompts/list";

    /// <summary>
    /// 由客户端发送以获取服务器提供的提示词的请求方法名称。<br/>
    /// The name of the request method sent by the client to get a prompt provided by the server.
    /// </summary>
    public const string PromptsGet = "prompts/get";

    /// <summary>
    /// 从客户端发送以请求服务器资源列表的请求方法名称。<br/>
    /// The name of the request method sent from the client to request a list of the server's resources.
    /// </summary>
    public const string ResourcesList = "resources/list";

    /// <summary>
    /// 从客户端发送以读取特定服务器资源的请求方法名称。<br/>
    /// The name of the request method sent from the client to read a specific server resource.
    /// </summary>
    public const string ResourcesRead = "resources/read";

    /// <summary>
    /// 从客户端发送以请求服务器资源模板列表的请求方法名称。<br/>
    /// The name of the request method sent from the client to request a list of the server's resource templates.
    /// </summary>
    public const string ResourcesTemplatesList = "resources/templates/list";

    /// <summary>
    /// 从客户端发送以请求在特定资源更改时从服务器接收
    /// notifications/resources/updated 通知的请求方法名称。<br/>
    /// The name of the request method sent from the client to request
    /// notifications/resources/updated
    /// notifications from the server whenever a particular resource changes.
    /// </summary>
    public const string ResourcesSubscribe = "resources/subscribe";

    /// <summary>
    /// 从客户端发送以请求取消订阅来自服务器的
    /// notifications/resources/updated 通知的请求方法名称。<br/>
    /// The name of the request method sent from the client to request unsubscribing from
    /// notifications/resources/updated notifications from the server.
    /// </summary>
    public const string ResourcesUnsubscribe = "resources/unsubscribe";

    /// <summary>
    /// 从服务器发送以请求客户端根目录列表的请求方法名称。<br/>
    /// The name of the request method sent from the server to request a list of the client's roots.
    /// </summary>
    public const string RootsList = "roots/list";

    /// <summary>
    /// 由任一端点发送以检查连接的端点是否仍然活动的请求方法名称。<br/>
    /// The name of the request method sent by either endpoint to check that the connected endpoint is still alive.
    /// </summary>
    public const string Ping = "ping";

    /// <summary>
    /// 从客户端发送到服务器以调整日志记录级别的请求方法名称。<br/>
    /// The name of the request method sent from the client to the server to adjust the logging level.
    /// </summary>
    /// <remarks>
    /// 此请求允许客户端通过设置最低严重性阈值来控制它们从服务器接收哪些日志消息。<br/>
    /// 处理此请求后，服务器将向客户端发送严重性达到或高于指定级别的日志消息，
    /// 作为 notifications/message 通知。<br/>
    /// This request allows clients to control which log messages they receive from the server
    /// by setting a minimum severity threshold. After processing this request, the server will
    /// send log messages with severity at or above the specified level to the client as
    /// notifications/message notifications.
    /// </remarks>
    public const string LoggingSetLevel = "logging/setLevel";

    /// <summary>
    /// 从客户端发送到服务器以请求完成建议的请求方法名称。<br/>
    /// The name of the request method sent from the client to the server to ask for completion suggestions.
    /// </summary>
    /// <remarks>
    /// 这用于为资源引用或提示词模板中的参数提供类似自动完成的功能。<br/>
    /// 客户端提供引用（资源或提示词）、参数名称和部分值，服务器响应匹配的完成选项。<br/>
    /// This is used to provide autocompletion-like functionality for arguments in
    /// a resource reference or a prompt template.
    /// The client provides a reference (resource or prompt), argument name, and partial value,
    /// and the server responds with matching completion options.
    /// </remarks>
    public const string CompletionComplete = "completion/complete";

    /// <summary>
    /// 从服务器发送以通过客户端对大型语言模型（LLM）进行采样的请求方法名称。<br/>
    /// The name of the request method sent from the server to sample a large language model (LLM) via the client.
    /// </summary>
    /// <remarks>
    /// 此请求允许服务器利用客户端侧可用的 LLM 根据提供的消息生成文本或图像响应。<br/>
    /// 它是 Model Context Protocol 中采样功能的一部分，使服务器能够访问客户端侧的 AI 模型，
    /// 而无需直接 API 访问这些模型。<br/>
    /// This request allows servers to utilize an LLM available on the client side to generate
    /// text or image responses based on provided messages. It is part of the sampling capability
    /// in the Model Context Protocol and enables servers to access client-side AI models without
    /// needing direct API access to those models.
    /// </remarks>
    public const string SamplingCreateMessage = "sampling/createMessage";

    /// <summary>
    /// 从客户端发送到服务器以通过客户端从用户引出额外信息的请求方法名称。<br/>
    /// The name of the request method sent from the client to the server to elicit additional information
    /// from the user via the client.
    /// </summary>
    /// <remarks>
    /// 当服务器需要更多信息才能继续执行任务或交互时，使用此请求。<br/>
    /// 服务器可以从用户请求结构化数据，并使用可选的 JSON schemas 来验证响应。<br/>
    /// This request is used when the server needs more information from the client to proceed with
    /// a task or interaction.
    /// Servers can request structured data from users, with optional JSON schemas to validate responses.
    /// </remarks>
    public const string ElicitationCreate = "elicitation/create";

    /// <summary>
    /// 客户端首次连接时发送到服务器以请求其初始化的请求方法名称。<br/>
    /// The name of the request method sent from the client to the server when it first connects,
    /// asking it initialize.
    /// </summary>
    /// <remarks>
    /// 初始化请求是客户端发送到服务器的第一个请求。<br/>
    /// 它在连接建立期间向服务器提供客户端信息和功能。<br/>
    /// 服务器响应其自己的功能和信息，建立会话的协议版本和可用功能。<br/>
    /// The initialize request is the first request sent by the client to the server.
    /// It provides client information and capabilities to the server during connection establishment.
    /// The server responds with its own capabilities and information, establishing the protocol version
    /// and available features for the session.
    /// </remarks>
    public const string Initialize = "initialize";

    /// <summary>
    /// 客户端在收到 initialize 响应后发送的通知，表示客户端已准备好开始正常操作。<br/>
    /// The notification sent by the client after receiving the initialize response,
    /// indicating that the client is ready to begin normal operations.
    /// </summary>
    /// <remarks>
    /// 这是一个通知（Notification），不需要服务器响应。<br/>
    /// 客户端必须在发送 initialize 请求并收到响应后发送此通知。<br/>
    /// 服务器在收到此通知之前不应发送除 ping 和 logging 之外的请求。<br/>
    /// This is a notification and does not expect a response from the server.<br/>
    /// The client MUST send this notification after sending the initialize request and receiving the response.<br/>
    /// The server SHOULD NOT send requests other than pings and logging before receiving this notification.
    /// </remarks>
    public const string NotificationsInitialized = "notifications/initialized";

    /// <summary>
    /// 获取任务详情的请求方法名称。<br/>
    /// The name of the request method to get task details.
    /// </summary>
    public const string TasksGet = "tasks/get";

    /// <summary>
    /// 获取任务执行结果的请求方法名称。<br/>
    /// The name of the request method to get task execution result.
    /// </summary>
    public const string TasksResult = "tasks/result";

    /// <summary>
    /// 取消任务的请求方法名称。<br/>
    /// The name of the request method to cancel a task.
    /// </summary>
    public const string TasksCancel = "tasks/cancel";

    /// <summary>
    /// 列出任务的请求方法名称。<br/>
    /// The name of the request method to list tasks.
    /// </summary>
    public const string TasksList = "tasks/list";

    /// <summary>
    /// 任务状态更新通知的方法名称。<br/>
    /// The name of the notification method for task status updates.
    /// </summary>
    public const string NotificationsTasksStatus = "notifications/tasks/status";

    /// <summary>
    /// 引出完成通知的方法名称。<br/>
    /// The name of the notification method for elicitation completion.
    /// </summary>
    public const string NotificationsElicitationComplete = "notifications/elicitation/complete";
}
