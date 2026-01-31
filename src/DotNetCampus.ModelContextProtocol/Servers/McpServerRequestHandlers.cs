using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP 服务器请求处理逻辑的基类。<br/>
/// 提供对标准 MCP 请求的处理逻辑，支持通过重写来实现自定义行为或埋点。<br/>
/// Base class for MCP server request handling logic.<br/>
/// Provides handling logic for standard MCP requests, supporting customization via overrides.
/// </summary>
/// <remarks>
/// <para>
/// 本类使用 "Full/Core" 双层设计模式：<br/>
/// - <b>Full 方法 (Handle...Async)</b>：协议合规层，负责参数验证、异常捕获与 Result 转换。重写以实现埋点。<br/>
/// - <b>Core 方法 (Execute...Async)</b>：业务执行层，负责核心逻辑。重写以实现行为拦截。
/// </para>
/// <para>
/// This class uses a "Full/Core" two-layer design pattern:<br/>
/// - <b>Full methods (Handle...Async)</b>: Protocol compliance layer, handles parameter validation, exception catching, and Result conversion. Override for instrumentation.<br/>
/// - <b>Core methods (Execute...Async)</b>: Business execution layer, handles core logic. Override for behavior interception.
/// </para>
/// </remarks>
public class McpServerRequestHandlers
{
    private readonly McpServer _server;

    /// <summary>
    /// 初始化 <see cref="McpServerRequestHandlers"/> 类的新实例。<br/>
    /// Initializes a new instance of the <see cref="McpServerRequestHandlers"/> class.
    /// </summary>
    /// <param name="server">MCP 服务器实例。<br/>The MCP server instance.</param>
    public McpServerRequestHandlers(McpServer server)
    {
        _server = server;
    }

    /// <summary>
    /// 获取 MCP 服务器实例。<br/>
    /// Gets the MCP server instance.
    /// </summary>
    protected McpServer Server => _server;

    // ========================================================================
    // Initialize
    // ========================================================================

    /// <summary>
    /// 处理初始化请求。<br/>
    /// Handles the initialize request.
    /// </summary>
    public virtual ValueTask<InitializeResult> HandleInitializeAsync(
        RequestContext<InitializeRequestParams> request,
        CancellationToken cancellationToken)
    {
        var hasTools = _server.Tools.Count > 0;
        var hasResources = _server.Resources.Count > 0;

        return ValueTask.FromResult(new InitializeResult
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
        });
    }

    // ========================================================================
    // Ping
    // ========================================================================

    /// <summary>
    /// 处理 ping 请求。<br/>
    /// Handles the ping request.
    /// </summary>
    public virtual ValueTask<EmptyObject> HandlePingAsync(
        RequestContext<PingRequestParams> request,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<EmptyObject>(default);
    }

    // ========================================================================
    // SetLoggingLevel
    // ========================================================================

    /// <summary>
    /// 处理设置日志级别请求。<br/>
    /// Handles the set logging level request.
    /// </summary>
    public virtual ValueTask<EmptyObject> HandleSetLoggingLevelAsync(
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

    // ========================================================================
    // ListTools
    // ========================================================================

    /// <summary>
    /// 处理列出工具请求。<br/>
    /// Handles the list tools request.
    /// </summary>
    public virtual ValueTask<ListToolsResult> HandleListToolsAsync(
        RequestContext<ListToolsRequestParams> request,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ListToolsResult
        {
            Tools = _server.Tools.Select(x => x.GetToolDefinition(CompiledSchemaJsonContext.Default)).ToList(),
        });
    }

    // ========================================================================
    // CallTool - Full/Core 模式
    // ========================================================================

    /// <summary>
    /// [Full Method] 处理工具调用请求。<br/>
    /// 此方法是协议层的入口，保证不抛出异常。所有错误都会被转换为 IsError=true 的 Result。<br/>
    /// <para>
    /// 重写此方法以实现：全局埋点、日志记录、审计。<br/>
    /// 注意：调用 base 方法后获得的 Result 对象是最终结果，无论是成功还是由异常转换而来。
    /// </para>
    /// [Full Method] Handles the tool call request.<br/>
    /// This method is the protocol-level entry point and guarantees no exceptions are thrown.<br/>
    /// All errors are converted to Result with IsError=true.
    /// </summary>
    public virtual async ValueTask<CallToolResult> HandleCallToolAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
    {
        var toolName = request.Params?.Name;

        if (string.IsNullOrEmpty(toolName))
        {
            return CallToolResult.FromError("Tool name is required.");
        }

        if (!_server.Tools.TryGet(toolName, out var tool))
        {
            return CallToolResult.FromError($"Unknown tool: {toolName}");
        }

        try
        {
            var arguments = request.Params?.Arguments ?? EmptyObject.JsonElement;
            var meta = request.Params?.Meta ?? EmptyObject.JsonElement;
            var jsonSerializer = _server.Context.JsonSerializer;
            var jsonContext = jsonSerializer switch
            {
                McpServerToolJsonSerializer mcpSerializer => mcpSerializer.JsonSerializerContext ?? CompiledSchemaJsonContext.Default,
                _ => CompiledSchemaJsonContext.Default,
            };

            var context = new McpServerCallToolContext
            {
                McpServer = _server,
                Services = request.Services,
                JsonSerializerContext = jsonContext,
                Meta = meta,
                Name = toolName,
                InputJsonArguments = arguments,
                CancellationToken = cancellationToken,
            };

            return await ExecuteCallToolAsync(tool, context);
        }
        catch (Exception ex)
        {
            // 异常兜底：转换为协议安全的错误结果
            return CreateCallToolErrorResult(ex);
        }
    }

    /// <summary>
    /// [Core Method] 执行工具业务逻辑。<br/>
    /// 重写此方法以实现：权限控制、行为拦截、Mock 数据。<br/>
    /// [Core Method] Executes the tool business logic.<br/>
    /// Override this method to implement: authorization, behavior interception, mock data.
    /// </summary>
    protected virtual ValueTask<CallToolResult> ExecuteCallToolAsync(
        IMcpServerTool tool,
        IMcpServerCallToolContext context)
    {
        return tool.CallTool(context);
    }

    /// <summary>
    /// 将异常转换为工具调用的错误结果。<br/>
    /// Converts an exception to a tool call error result.
    /// </summary>
    protected virtual CallToolResult CreateCallToolErrorResult(Exception ex)
    {
        var errorMessage = _server.Context.IsDebugMode
            ? McpExceptionData.From(ex).ToJsonString()
            : ex.Message;
        return CallToolResult.FromError(errorMessage);
    }

    // ========================================================================
    // ListResources
    // ========================================================================

    /// <summary>
    /// 处理列出资源请求。<br/>
    /// Handles the list resources request.
    /// </summary>
    public virtual ValueTask<ListResourcesResult> HandleListResourcesAsync(
        RequestContext<ListResourcesRequestParams> request,
        CancellationToken cancellationToken)
    {
        var jsonContext = CompiledSchemaJsonContext.Default;
        var resources = _server.Resources.GetStaticResources()
            .Select(r => (Resource)r.GetResourceDefinition(jsonContext))
            .ToArray();

        return ValueTask.FromResult(new ListResourcesResult
        {
            Resources = resources,
        });
    }

    // ========================================================================
    // ListResourceTemplates
    // ========================================================================

    /// <summary>
    /// 处理列出资源模板请求。<br/>
    /// Handles the list resource templates request.
    /// </summary>
    public virtual ValueTask<ListResourceTemplatesResult> HandleListResourceTemplatesAsync(
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

    // ========================================================================
    // ReadResource - Full/Core 模式
    // ========================================================================

    /// <summary>
    /// [Full Method] 处理读取资源请求。<br/>
    /// 此方法包含异常处理逻辑。<br/>
    /// [Full Method] Handles the read resource request.<br/>
    /// This method contains exception handling logic.
    /// </summary>
    /// <exception cref="McpResourceNotFoundException">当资源未找到时抛出。</exception>
    public virtual async ValueTask<ReadResourceResult> HandleReadResourceAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken)
    {
        var uri = request.Params?.Uri;

        if (string.IsNullOrEmpty(uri))
        {
            throw new ArgumentException("Resource URI is required.", nameof(uri));
        }

        if (!_server.Resources.TryRoute(uri, out var resource, out var parameters))
        {
            throw new McpResourceNotFoundException(uri);
        }

        var meta = request.Params?.Meta ?? EmptyObject.JsonElement;
        var jsonSerializer = _server.Context.JsonSerializer;
        var jsonContext = jsonSerializer switch
        {
            McpServerToolJsonSerializer mcpSerializer => mcpSerializer.JsonSerializerContext ?? CompiledSchemaJsonContext.Default,
            _ => CompiledSchemaJsonContext.Default,
        };

        var context = new McpServerReadResourceContext
        {
            McpServer = _server,
            Services = request.Services,
            JsonSerializerContext = jsonContext,
            Meta = meta,
            Uri = uri,
            MimeType = resource.MimeType,
        };

        return await ExecuteReadResourceAsync(resource, context);
    }

    /// <summary>
    /// [Core Method] 执行读取资源核心逻辑。<br/>
    /// 重写此方法以实现：权限控制、资源重定向。<br/>
    /// [Core Method] Executes the read resource core logic.<br/>
    /// Override this method to implement: authorization, resource redirection.
    /// </summary>
    protected virtual ValueTask<ReadResourceResult> ExecuteReadResourceAsync(
        IMcpServerResource resource,
        IMcpServerReadResourceContext context)
    {
        return resource.ReadResource(context);
    }
}
