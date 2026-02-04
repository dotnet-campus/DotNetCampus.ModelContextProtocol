using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP 服务器请求处理逻辑的基类。<br/>
/// 通过继承此类并重写方法，可以实现自定义的请求处理、记录或扩展功能。
/// </summary>
public class McpServerRequestHandlers
{
    private readonly McpServer _server;

    /// <summary>
    /// 初始化 <see cref="McpServerRequestHandlers"/> 的新实例。
    /// </summary>
    public McpServerRequestHandlers(McpServer server)
    {
        _server = server;
    }

    /// <summary>
    /// 获取 MCP 服务器实例。
    /// </summary>
    protected McpServer Server => _server;

    /// <summary>
    /// 获取日志记录器。
    /// </summary>
    protected IMcpLogger Logger => _server.Context.Logger;

    #region Initialize

    /// <summary>
    /// 处理初始化请求的协议入口。
    /// </summary>
    internal async ValueTask<InitializeResult> HandleInitializeAsync(
        RequestContext<InitializeRequestParams> request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await InitializeAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Error($"[McpServer][Mcp] HandleInitializeAsync failed. Error={ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 处理初始化请求。重写此方法以实现自定义初始化逻辑。
    /// </summary>
    public virtual ValueTask<InitializeResult> InitializeAsync(
        RequestContext<InitializeRequestParams> request,
        CancellationToken cancellationToken)
    {
        var clientInfo = request.Params?.ClientInfo;
        Logger.Info($"[McpServer][Mcp] Client initializing. ClientName={clientInfo?.Name}, ClientVersion={clientInfo?.Version}, ProtocolVersion={request.Params?.ProtocolVersion}");

        var hasTools = _server.Tools.Count > 0;
        var hasResources = _server.Resources.Count > 0;

        var result = new InitializeResult
        {
            ProtocolVersion = ProtocolVersion.Current,
            ServerInfo = new Implementation
            {
                Name = _server.ServerName,
                Version = _server.ServerVersion,
            },
            Instructions = _server.Instructions,
            Capabilities = new ServerCapabilities
            {
                Tools = hasTools
                    ? new ToolsCapability { ListChanged = false }
                    : null,
                Resources = hasResources
                    ? new ResourcesCapability { Subscribe = false, ListChanged = false }
                    : null,
                Prompts = null,
                Logging = EmptyObject.JsonElement,
            },
        };

        Logger.Info($"[McpServer][Mcp] Server initialized. ServerName={_server.ServerName}, ServerVersion={_server.ServerVersion}, ToolCount={_server.Tools.Count}, ResourceCount={_server.Resources.Count}");

        return ValueTask.FromResult(result);
    }

    #endregion

    #region Ping

    /// <summary>
    /// 处理 ping 请求的协议入口。
    /// </summary>
    internal async ValueTask<EmptyObject> HandlePingAsync(
        RequestContext<PingRequestParams> request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await PingAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Error($"[McpServer][Mcp] HandlePingAsync failed. Error={ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 处理 ping 请求。重写此方法以实现自定义 ping 响应。
    /// </summary>
    public virtual ValueTask<EmptyObject> PingAsync(
        RequestContext<PingRequestParams> request,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<EmptyObject>(default);
    }

    #endregion

    #region SetLoggingLevel

    /// <summary>
    /// 处理设置日志级别请求的协议入口。
    /// </summary>
    internal async ValueTask<EmptyObject> HandleSetLoggingLevelAsync(
        RequestContext<SetLevelRequestParams> request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SetLoggingLevelAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Error($"[McpServer][Mcp] HandleSetLoggingLevelAsync failed. Error={ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 处理设置日志级别请求。重写此方法以实现自定义日志级别处理。
    /// </summary>
    public virtual ValueTask<EmptyObject> SetLoggingLevelAsync(
        RequestContext<SetLevelRequestParams> request,
        CancellationToken cancellationToken)
    {
        if (request.Params is null)
        {
            throw new ArgumentNullException(nameof(request.Params), "SetLevelRequestParams is required.");
        }

        _server.Context.McpLoggingLevel = request.Params.Level;
        return ValueTask.FromResult<EmptyObject>(default);
    }

    #endregion

    #region ListTools

    /// <summary>
    /// 处理列出工具请求的协议入口。
    /// </summary>
    internal async ValueTask<ListToolsResult> HandleListToolsAsync(
        RequestContext<ListToolsRequestParams> request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ListToolsAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Error($"[McpServer][Mcp] HandleListToolsAsync failed. Error={ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 处理列出工具请求。重写此方法以实现自定义工具列表。
    /// </summary>
    public virtual ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> request,
        CancellationToken cancellationToken)
    {
        var tools = _server.Tools.Select(x => x.GetToolDefinition(CompiledSchemaJsonContext.Default)).ToList();
        Logger.Debug($"[McpServer][Mcp] Listing tools. Count={tools.Count}");
        return ValueTask.FromResult(new ListToolsResult
        {
            Tools = tools,
        });
    }

    #endregion

    #region CallTool

    /// <summary>
    /// 处理工具调用请求的协议入口。解析参数并调用 <see cref="CallToolAsync"/>。
    /// </summary>
    internal async ValueTask<CallToolResult> HandleCallToolAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
    {
        var toolName = request.Params?.Name;
        var tool = toolName is null
            ? null
            : _server.Tools.TryGet(toolName, out var t)
                ? t
                : null;
        var context = toolName is null
            ? null
            : new McpServerCallToolContext
            {
                McpServer = _server,
                Services = request.Services,
                JsonSerializerContext = _server.Context.JsonSerializer switch
                {
                    McpServerToolJsonSerializer mcpSerializer => mcpSerializer.JsonSerializerContext ?? CompiledSchemaJsonContext.Default,
                    _ => CompiledSchemaJsonContext.Default,
                },
                Meta = request.Params?.Meta ?? EmptyObject.JsonElement,
                Name = toolName,
                InputJsonArguments = request.Params?.Arguments ?? EmptyObject.JsonElement,
                CancellationToken = cancellationToken,
            };

        try
        {
            return await CallToolAsync(request, toolName, tool, context);
        }
        catch (Exception ex)
        {
            Logger.Error($"[McpServer][Mcp] HandleCallToolAsync failed. Error={ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 处理工具调用请求。此方法保证不抛出异常，所有错误都转换为 IsError=true 的 Result。<br/>
    /// 重写此方法以实现全局的记录或扩展处理。
    /// </summary>
    /// <param name="rawRequest">原始请求上下文。重写此方法时，如果需要可以使用。</param>
    /// <param name="toolName">请求的工具名称，如果为 <see langword="null"/>，则表示请求中未提供工具名称。</param>
    /// <param name="tool">要调用的工具，如果为 <see langword="null"/> 表示未找到请求名称的工具。</param>
    /// <param name="context">工具调用上下文。当 <paramref name="tool"/> 非空时，此参数也非空。</param>
    /// <returns>
    /// 工具调用的结果。如果调用过程中发生了异常，则工具结果表示错误，且会在 <see cref="CallToolResult.RawException"/> 中保存所发生的异常。
    /// </returns>
    public virtual async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> rawRequest,
        string? toolName,
        IMcpServerTool? tool,
        IMcpServerCallToolContext? context)
    {
        // 验证工具名称。
        if (string.IsNullOrEmpty(toolName))
        {
            Logger.Warn($"[McpServer][Mcp] Tool call rejected: Tool name is required.");
            return CallToolResult.FromError("Tool name is required.");
        }

        // 验证工具是否存在。
        if (tool is null)
        {
            Logger.Warn($"[McpServer][Mcp] Tool call rejected: Unknown tool. ToolName={toolName}");
            return CallToolResult.FromError($"Unknown tool: {toolName}");
        }

        Logger.Info($"[McpServer][Mcp] Calling tool. ToolName={toolName}");

        try
        {
            var result = await CallToolCoreAsync(tool, context!);
            if (result.IsError == true)
            {
                Logger.Warn($"[McpServer][Mcp] Tool call completed with error. ToolName={toolName}");
            }
            else
            {
                Logger.Debug($"[McpServer][Mcp] Tool call completed successfully. ToolName={toolName}");
            }
            return result;
        }
        catch (McpToolMissingRequiredArgumentException ex)
        {
            // 调用工具时缺少必要的参数。
            Logger.Warn($"[McpServer][Mcp] Tool call failed: Missing required argument. ToolName={toolName}, Error={ex.Message}");
            return CallToolResult.FromException(ex);
        }
        catch (McpToolMissingRequiredTypeDiscriminatorException ex)
        {
            // 调用工具时缺少必要的类型鉴别器。
            Logger.Warn($"[McpServer][Mcp] Tool call failed: Missing type discriminator. ToolName={toolName}, Error={ex.Message}");
            return CallToolResult.FromException(ex);
        }
        catch (JsonException ex)
        {
            // 调用工具时传入的参数无法被正确反序列化。
            Logger.Warn($"[McpServer][Mcp] Tool call failed: JSON deserialization error. ToolName={toolName}, Error={ex.Message}");
            return CallToolResult.FromException(ex, $"Failed to deserialize tool arguments: {ex.Message}");
        }
        catch (McpToolServiceNotFoundException ex)
        {
            // 调用工具时缺少必要的服务。
            Logger.Error($"[McpServer][Mcp] Tool call failed: Service not found. ToolName={toolName}, Error={ex.Message}");
            return CallToolResult.FromException(ex);
        }
        catch (McpToolJsonTypeInfoNotFoundException ex)
        {
            // 给开发者查看的错误，提示开发者生成缺失的 JsonTypeInfo。
            Logger.Error($"[McpServer][Mcp] Tool call failed: JsonTypeInfo not found. ToolName={toolName}, Error={ex.Message}");
            return CallToolResult.FromException(ex);
        }
        catch (McpToolUsageException ex)
        {
            // 业务端认为工具使用不正确，而且已经在 Message 中提供了 AI 可读的错误信息。
            Logger.Warn($"[McpServer][Mcp] Tool call failed: Usage error. ToolName={toolName}, Error={ex.Message}");
            return CallToolResult.FromException(ex);
        }
        catch (McpCallToolFailedException ex)
        {
            // 程序内部状态或其他错误导致工具执行失败。
            Logger.Warn($"[McpServer][Mcp] Tool call failed. ToolName={toolName}, Error={ex.Message}");
            return CallToolResult.FromException(ex);
        }
        catch (Exception ex)
        {
            // 其他未知错误。
            Logger.Error($"[McpServer][Mcp] Tool call failed: Unexpected error. ToolName={toolName}, Error={ex.Message}");
            return _server.Context.IsDebugMode
                ? CallToolResult.FromException(ex, McpExceptionData.From(ex).ToJsonString())
                : CallToolResult.FromException(ex);
        }
    }

    /// <summary>
    /// 执行工具调用的核心逻辑。重写此方法以实现访问控制、行为拦截或 Mock 数据。
    /// </summary>
    /// <param name="tool">要调用的工具（已验证非空）。</param>
    /// <param name="context">工具调用上下文。</param>
    protected virtual ValueTask<CallToolResult> CallToolCoreAsync(
        IMcpServerTool tool,
        IMcpServerCallToolContext context)
    {
        return tool.CallTool(context);
    }

    #endregion

    #region ListResources

    /// <summary>
    /// 处理列出资源请求的协议入口。
    /// </summary>
    internal async ValueTask<ListResourcesResult> HandleListResourcesAsync(
        RequestContext<ListResourcesRequestParams> request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ListResourcesAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Error($"[McpServer][Mcp] HandleListResourcesAsync failed. Error={ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 处理列出资源请求。重写此方法以实现自定义资源列表。
    /// </summary>
    public virtual ValueTask<ListResourcesResult> ListResourcesAsync(
        RequestContext<ListResourcesRequestParams> request,
        CancellationToken cancellationToken)
    {
        var jsonContext = CompiledSchemaJsonContext.Default;
        var resources = _server.Resources.GetStaticResources()
            .Select(r => (Resource)r.GetResourceDefinition(jsonContext))
            .ToArray();

        Logger.Debug($"[McpServer][Mcp] Listing resources. Count={resources.Length}");

        return ValueTask.FromResult(new ListResourcesResult
        {
            Resources = resources,
        });
    }

    #endregion

    #region ListResourceTemplates

    /// <summary>
    /// 处理列出资源模板请求的协议入口。
    /// </summary>
    internal async ValueTask<ListResourceTemplatesResult> HandleListResourceTemplatesAsync(
        RequestContext<ListResourceTemplatesRequestParams> request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ListResourceTemplatesAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Error($"[McpServer][Mcp] HandleListResourceTemplatesAsync failed. Error={ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 处理列出资源模板请求。重写此方法以实现自定义资源模板列表。
    /// </summary>
    public virtual ValueTask<ListResourceTemplatesResult> ListResourceTemplatesAsync(
        RequestContext<ListResourceTemplatesRequestParams> request,
        CancellationToken cancellationToken)
    {
        var jsonContext = CompiledSchemaJsonContext.Default;
        var templates = _server.Resources.GetTemplateResources()
            .Select(r => (ResourceTemplate)r.GetResourceDefinition(jsonContext))
            .ToArray();

        return ValueTask.FromResult(new ListResourceTemplatesResult
        {
            ResourceTemplates = templates,
        });
    }

    #endregion

    #region ReadResource

    /// <summary>
    /// 处理读取资源请求的协议入口。解析参数并调用 <see cref="ReadResourceAsync"/>。
    /// </summary>
    internal async ValueTask<ReadResourceResult> HandleReadResourceAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken)
    {
        var uri = request.Params?.Uri;
        var resource = uri is null
            ? null
            : _server.Resources.TryRoute(uri, out var r, out var parameters)
                ? r
                : null;
        var context = uri is null
            ? null
            : new McpServerReadResourceContext
            {
                McpServer = _server,
                Services = request.Services,
                JsonSerializerContext = _server.Context.JsonSerializer switch
                {
                    McpServerToolJsonSerializer mcpSerializer => mcpSerializer.JsonSerializerContext ?? CompiledSchemaJsonContext.Default,
                    _ => CompiledSchemaJsonContext.Default,
                },
                Meta = request.Params?.Meta ?? EmptyObject.JsonElement,
                Uri = uri,
                MimeType = resource?.MimeType,
            };

        try
        {
            return await ReadResourceAsync(request, uri, resource, context);
        }
        catch (Exception ex)
        {
            Logger.Error($"[McpServer][Mcp] HandleReadResourceAsync failed. Error={ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 处理读取资源请求。重写此方法以实现全局的记录或扩展处理。
    /// </summary>
    /// <param name="rawRequest">原始请求上下文。重写此方法时，如果需要可以使用。</param>
    /// <param name="uri">请求的资源 URI，如果为 <see langword="null"/>，则表示请求中未提供资源 URI。</param>
    /// <param name="resource">要读取的资源，如果为 <see langword="null"/> 表示未找到请求 URI 的资源。</param>
    /// <param name="context">资源读取上下文。当 <paramref name="resource"/> 非空时，此参数也非空。</param>
    public virtual async ValueTask<ReadResourceResult> ReadResourceAsync(
        RequestContext<ReadResourceRequestParams> rawRequest,
        string? uri,
        IMcpServerResource? resource,
        IMcpServerReadResourceContext? context)
    {
        // 验证资源 URI。
        if (string.IsNullOrEmpty(uri))
        {
            Logger.Warn($"[McpServer][Mcp] Resource read rejected: URI is required.");
            throw new McpServerException("Resource URI is required.");
        }

        // 验证资源是否存在。
        if (resource is null)
        {
            Logger.Warn($"[McpServer][Mcp] Resource read rejected: Resource not found. Uri={uri}");
            throw new McpServerException($"Resource not found: {uri}");
        }

        Logger.Debug($"[McpServer][Mcp] Reading resource. Uri={uri}");

        try
        {
            var result = await ReadResourceCoreAsync(resource, context!);
            Logger.Debug($"[McpServer][Mcp] Resource read completed. Uri={uri}");
            return result;
        }
        catch (McpResourceNotFoundException ex)
        {
            Logger.Warn($"[McpServer][Mcp] Resource read failed: Resource not found. Uri={uri}");
            throw new McpServerException("Resource not found.", ex);
        }
        catch (Exception ex)
        {
            throw new McpServerException("ReadResource failed.", ex);
        }
    }

    /// <summary>
    /// 执行读取资源的核心逻辑。重写此方法以实现访问控制或资源重定向。
    /// </summary>
    /// <param name="resource">要读取的资源（已验证非空）。</param>
    /// <param name="context">资源读取上下文。</param>
    protected virtual ValueTask<ReadResourceResult> ReadResourceCoreAsync(
        IMcpServerResource resource,
        IMcpServerReadResourceContext context)
    {
        return resource.ReadResource(context);
    }

    #endregion

    #region 全局处理

    /// <summary>
    /// 在请求处理前调用。重写此方法以实现全局的请求预处理或记录。
    /// </summary>
    /// <param name="request">收到的 JSON-RPC 请求。</param>
    protected internal virtual ValueTask OnRequestReceivingAsync(JsonRpcRequest request) => default;

    /// <summary>
    /// 在通知收到后调用。通知不需要响应。重写此方法以实现通知的记录或处理。
    /// </summary>
    /// <param name="notification">收到的 JSON-RPC 通知（id 为 <see langword="null"/> 的请求）。</param>
    protected internal virtual ValueTask OnNotificationReceivedAsync(JsonRpcRequest notification) => default;

    /// <summary>
    /// 在响应发送后调用。重写此方法以实现全局的响应后处理或记录。
    /// </summary>
    /// <param name="request">原始请求。</param>
    /// <param name="response">发送的响应。</param>
    protected internal virtual ValueTask OnResponseSentAsync(JsonRpcRequest request, JsonRpcResponse response) => default;

    #endregion
}
