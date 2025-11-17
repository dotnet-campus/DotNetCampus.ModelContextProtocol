using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Core;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP 服务器处理来自客户端所有请求的处理器方法合集。
/// </summary>
/// <param name="server">MCP 服务器实例。</param>
public class McpRequestHandlers(McpServer server)
{
    /// <summary>
    /// 获取最基本的 MCP 请求相应的处理器集合。<br/>
    /// 通过调用该属性内的方法而不是本类型的方法，可以绕过异常处理逻辑，直接获得底层的请求处理能力。
    /// </summary>
    public readonly McpRawRequestHandlers Raw = new(server);

    /// <summary>
    /// 获取或设置初始化请求处理程序。
    /// </summary>
    [NotNull]
    public McpRequestHandler<InitializeRequestParams, InitializeResult>? InitializeHandler
    {
        get => field ?? Raw.Initialize;
        set;
    }

    /// <summary>
    /// 处理初始化请求。
    /// </summary>
    public async ValueTask<InitializeResult> Initialize(RequestContext<InitializeRequestParams> request, CancellationToken cancellationToken)
    {
        try
        {
            return await InitializeHandler(request, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ModelContextProtocolException($"Initialization failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取或设置 Ping 请求处理程序。
    /// </summary>
    [NotNull]
    public McpRequestHandler<PingRequestParams, EmptyResult>? PingHandler
    {
        get => field ?? Raw.Ping;
        set;
    }

    /// <summary>
    /// 处理 Ping 请求。
    /// </summary>
    public async ValueTask<EmptyResult> Ping(RequestContext<PingRequestParams> request, CancellationToken cancellationToken)
    {
        try
        {
            return await PingHandler(request, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ModelContextProtocolException($"Ping failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取或设置列出工具请求处理程序。
    /// </summary>
    [NotNull]
    public McpRequestHandler<ListToolsRequestParams, ListToolsResult>? ListToolsHandler
    {
        get => field ?? Raw.ListTools;
        set;
    }

    /// <summary>
    /// 处理列出工具请求。
    /// </summary>
    public async ValueTask<ListToolsResult> ListTools(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
    {
        try
        {
            return await ListToolsHandler(request, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ModelContextProtocolException("ListTools failed.", ex);
        }
    }

    /// <summary>
    /// 获取或设置调用工具请求处理程序。
    /// </summary>
    [NotNull]
    public McpRequestHandler<CallToolRequestParams, CallToolResult>? CallToolHandler
    {
        get => field ?? Raw.CallTool;
        set;
    }

    /// <summary>
    /// 处理调用工具请求。
    /// </summary>
    public async ValueTask<CallToolResult> CallTool(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
    {
        try
        {
            return await CallToolHandler(request, cancellationToken);
        }
        catch (McpToolMissingRequiredArgumentException ex)
        {
            // 调用工具时缺少必要的参数。
            return CallToolResult.FromError(ex.Message);
        }
        catch (McpToolMissingRequiredTypeDiscriminatorException ex)
        {
            // 调用工具时缺少必要的类型鉴别器。
            return CallToolResult.FromError(ex.Message);
        }
        catch (JsonException ex)
        {
            // 调用工具时传入的参数无法被正确反序列化。
            return CallToolResult.FromError($"Failed to deserialize tool arguments: {ex.Message}");
        }
        catch (McpToolServiceNotFoundException ex)
        {
            // 调用工具时缺少必要的服务。
            return CallToolResult.FromError(ex.Message);
        }
        catch (McpToolJsonTypeInfoNotFoundException ex)
        {
            // 给开发者查看的错误，提示开发者生成缺失的 JsonTypeInfo。
            return CallToolResult.FromError(ex.Message);
        }
        catch (McpToolUsageException ex)
        {
            // 业务端认为工具使用不正确，而且已经在 Message 中提供了 AI 可读的错误信息。
            return CallToolResult.FromError(ex.Message);
        }
        catch (Exception ex)
        {
            // 其他未知错误。
            return server.Context.IsDebugMode
                ? CallToolResult.FromError(McpExceptionData.From(ex).ToJsonString())
                : CallToolResult.FromError(ex.Message);
        }
    }

    // public McpRequestHandler<ListPromptsRequestParams, ListPromptsResult>? ListPromptsHandler { get; set; }
    //
    // public McpRequestHandler<GetPromptRequestParams, GetPromptResult>? GetPromptHandler { get; set; }
    //
    // public McpRequestHandler<ListResourceTemplatesRequestParams, ListResourceTemplatesResult>? ListResourceTemplatesHandler { get; set; }
    //
    // public McpRequestHandler<ListResourcesRequestParams, ListResourcesResult>? ListResourcesHandler { get; set; }
    //
    // public McpRequestHandler<ReadResourceRequestParams, ReadResourceResult>? ReadResourceHandler { get; set; }
    //
    // public McpRequestHandler<CompleteRequestParams, CompleteResult>? CompleteHandler { get; set; }
    //
    // public McpRequestHandler<SubscribeRequestParams, EmptyResult>? SubscribeToResourcesHandler { get; set; }
    //
    // public McpRequestHandler<UnsubscribeRequestParams, EmptyResult>? UnsubscribeFromResourcesHandler { get; set; }

    /// <summary>
    /// 获取或设置日志级别设置请求处理程序
    /// </summary>
    [NotNull]
    public McpRequestHandler<SetLevelRequestParams, EmptyResult>? SetLoggingLevelHandler
    {
        get => field ?? Raw.SetLoggingLevel;
        set;
    }

    /// <summary>
    /// 处理设置日志级别请求。
    /// </summary>
    public async ValueTask<EmptyResult> SetLoggingLevel(RequestContext<SetLevelRequestParams> request, CancellationToken cancellationToken)
    {
        try
        {
            return await SetLoggingLevelHandler(request, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ModelContextProtocolException("SetLoggingLevel failed.", ex);
        }
    }

    /// <summary>
    /// 获取或设置通知处理程序列表。
    /// </summary>
    public IEnumerable<KeyValuePair<string, Func<JsonRpcNotification, CancellationToken, ValueTask>>>? NotificationHandlers { get; set; }
}

/// <summary>
/// MCP 请求处理程序委托。
/// </summary>
/// <typeparam name="TParams">请求参数类型</typeparam>
/// <typeparam name="TResult">响应结果类型</typeparam>
public delegate ValueTask<TResult> McpRequestHandler<TParams, TResult>(
    RequestContext<TParams> request,
    CancellationToken cancellationToken);
