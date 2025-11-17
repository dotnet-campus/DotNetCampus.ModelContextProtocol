using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Core;
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

        return new InitializeResult
        {
            ProtocolVersion = "2025-06-18",
            ServerInfo = new ServerInfo
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
                // 暂不支持 prompts 和 resources
                Prompts = null,
                Resources = null,
                // 支持日志记录
                Logging = EmptyResult.JsonElement,
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

        var arguments = request.Params?.Arguments ?? default;
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
            InputJsonArguments = arguments,
            CancellationToken = cancellationToken,
        };
        return await tool.CallTool(context);
    }

    /// <summary>
    /// 处理 ping 请求。
    /// </summary>
    public async ValueTask<EmptyResult> Ping(RequestContext<PingRequestParams> request, CancellationToken cancellationToken)
    {
        return default;
    }

    /// <summary>
    /// 处理设置日志级别请求。
    /// </summary>
    public async ValueTask<EmptyResult> SetLoggingLevel(RequestContext<SetLevelRequestParams> request, CancellationToken cancellationToken)
    {
        if (request.Params is null)
        {
            throw new ArgumentNullException(nameof(request.Params), "SetLevelRequestParams is required.");
        }

        // 更新服务器上下文中的日志级别
        server.Context.ClientLoggingLevel = request.Params.Level;

        return default;
    }
}
