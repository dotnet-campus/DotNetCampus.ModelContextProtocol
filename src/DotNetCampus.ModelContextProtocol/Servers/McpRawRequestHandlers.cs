using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP 服务器处理来自客户端所有请求的处理器方法合集。<br/>
/// 与 <see cref="McpRequestHandlers"/> 不同，本类型只处理 MCP 请求的最初响应，不做任何异常处理。
/// 因此，你可以通过调用它来实现自定义的异常处理逻辑。
/// </summary>
/// <param name="server">MCP 服务器实例。</param>
public class McpRawRequestHandlers(McpServer server)
{
    /// <summary>
    /// 处理初始化请求。
    /// </summary>
    public async ValueTask<InitializeResult> Initialize(RequestContext<InitializeRequestParams> request, CancellationToken cancellationToken)
    {
        var hasTools = server.Tools.Count > 0;
        var hasResources = server.Resources.Count > 0;

        return new InitializeResult
        {
            ProtocolVersion = ProtocolVersion.Current,
            ServerInfo = new Implementation
            {
                Name = server.ServerName,
                Version = server.ServerVersion,
            },
            Instructions = server.Instructions,
            Capabilities = new ServerCapabilities
            {
                // 如果服务器有工具,则声明支持 tools 能力
                Tools = hasTools
                    ? new ToolsCapability
                    {
                        ListChanged = false,
                    }
                    : null,
                // 如果服务器有资源,则声明支持 resources 能力
                Resources = hasResources
                    ? new ResourcesCapability
                    {
                        Subscribe = false,
                        ListChanged = false,
                    }
                    : null,
                // 暂不支持 prompts
                Prompts = null,
                // 支持日志记录
                Logging = EmptyObject.JsonElement,
            },
        };
    }

    /// <summary>
    /// 处理列出工具请求。
    /// </summary>
    public async ValueTask<ListToolsResult> ListTools(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
    {
        return new ListToolsResult
        {
            Tools = server.Tools.Select(x => x.GetToolDefinition(CompiledSchemaJsonContext.Default)).ToList(),
        };
    }

    /// <summary>
    /// 处理调用工具请求。
    /// </summary>
    public async ValueTask<CallToolResult> CallTool(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
    {
        var toolName = request.Params?.Name;

        if (string.IsNullOrEmpty(toolName))
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = "Tool name is required." }],
            };
        }

        if (!server.Tools.TryGet(toolName, out var tool))
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = $"Unknown tool: {toolName}" }],
            };
        }

        var arguments = request.Params?.Arguments ?? EmptyObject.JsonElement;
        var meta = request.Params?.Meta ?? EmptyObject.JsonElement;
        var jsonSerializer = server.Context.JsonSerializer;
        var jsonContext = jsonSerializer switch
        {
            McpServerToolJsonSerializer mcpSerializer => mcpSerializer.JsonSerializerContext ?? CompiledSchemaJsonContext.Default,
            _ => CompiledSchemaJsonContext.Default,
        };

        var context = new McpServerCallToolContext
        {
            McpServer = server,
            Services = request.Services,
            JsonSerializerContext = jsonContext,
            Meta = meta,
            Name = toolName,
            InputJsonArguments = arguments,
            CancellationToken = cancellationToken,
        };
        return await tool.CallTool(context);
    }

    /// <summary>
    /// 处理 ping 请求。
    /// </summary>
    public async ValueTask<EmptyObject> Ping(RequestContext<PingRequestParams> request, CancellationToken cancellationToken)
    {
        return default;
    }

    /// <summary>
    /// 处理设置日志级别请求。
    /// </summary>
    public async ValueTask<EmptyObject> SetLoggingLevel(RequestContext<SetLevelRequestParams> request, CancellationToken cancellationToken)
    {
        if (request.Params is null)
        {
            throw new ArgumentNullException(nameof(request.Params), "SetLevelRequestParams is required.");
        }

        // 更新服务器上下文中的日志级别
        server.Context.McpLoggingLevel = request.Params.Level;

        return default;
    }

    /// <summary>
    /// 处理列出资源请求。
    /// </summary>
    public async ValueTask<ListResourcesResult> ListResources(RequestContext<ListResourcesRequestParams> request, CancellationToken cancellationToken)
    {
        var jsonContext = CompiledSchemaJsonContext.Default;
        var resources = server.Resources.GetStaticResources()
            .Select(r => (Resource)r.GetResourceDefinition(jsonContext))
            .ToArray();

        return new ListResourcesResult
        {
            Resources = resources,
        };
    }

    /// <summary>
    /// 处理列出资源模板请求。
    /// </summary>
    public async ValueTask<ListResourceTemplatesResult> ListResourceTemplates(RequestContext<ListResourceTemplatesRequestParams> request,
        CancellationToken cancellationToken)
    {
        var jsonContext = CompiledSchemaJsonContext.Default;
        var templates = server.Resources.GetTemplateResources()
            .Select(r => (ResourceTemplate)r.GetResourceDefinition(jsonContext))
            .ToArray();

        return new ListResourceTemplatesResult
        {
            ResourceTemplates = templates,
        };
    }

    /// <summary>
    /// 处理读取资源请求。
    /// </summary>
    public async ValueTask<ReadResourceResult> ReadResource(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken)
    {
        var uri = request.Params?.Uri;

        if (string.IsNullOrEmpty(uri))
        {
            throw new ArgumentException("Resource URI is required.", nameof(uri));
        }

        // 通过 URI 路由找到匹配的资源
        if (!server.Resources.TryRoute(uri, out var resource, out var parameters))
        {
            // 根据 MCP 官方规范 § 7 Error Handling：
            // Resource not found: -32002
            // 抛出异常，由 McpRequestHandlers 层将其转换为 JSON-RPC 错误 (-32002)
            throw new InvalidOperationException($"Resource not found: {uri}");
        }

        var meta = request.Params?.Meta ?? EmptyObject.JsonElement;
        var jsonSerializer = server.Context.JsonSerializer;
        var jsonContext = jsonSerializer switch
        {
            McpServerToolJsonSerializer mcpSerializer => mcpSerializer.JsonSerializerContext ?? CompiledSchemaJsonContext.Default,
            _ => CompiledSchemaJsonContext.Default,
        };

        var context = new McpServerReadResourceContext
        {
            McpServer = server,
            Services = request.Services,
            JsonSerializerContext = jsonContext,
            Meta = meta,
            Uri = uri,
            MimeType = resource.MimeType,
        };

        // 如果是模板资源，需要将参数注入到上下文（可通过扩展属性实现）
        // 当前版本简化处理，参数由业务代码自行从 Uri 中解析

        return await resource.ReadResource(context);
    }
}
