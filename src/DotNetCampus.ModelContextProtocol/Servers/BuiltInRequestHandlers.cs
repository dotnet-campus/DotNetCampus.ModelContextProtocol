using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Core;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Servers;

public class BuiltInRequestHandlers(McpServer server)
{
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

    public async ValueTask<ListToolsResult> ListTools(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
    {
        return new ListToolsResult
        {
            Tools = server.Tools.Select(x => x.GetToolDefinition(InputSchemaJsonContext.Default)).ToList(),
        };
    }

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
                Content = [new TextContentBlock { Text = $"Tool '{toolName}' not found." }],
            };
        }

        var arguments = request.Params?.Arguments ?? default;
        var jsonSerializer = server.Context.JsonSerializer;
        var jsonContext = jsonSerializer switch
        {
            McpServerToolJsonSerializer mcpSerializer => mcpSerializer.JsonSerializerContext ?? InputSchemaJsonContext.Default,
            _ => InputSchemaJsonContext.Default,
        };

        var context = new McpServerCallToolContext
        {
            McpServer = server,
            Services = new EmptyServiceProvider(),
            JsonSerializerContext = jsonContext,
            InputJsonArguments = arguments,
            CancellationToken = cancellationToken,
        };
        return await tool.CallTool(context);
    }

    public async ValueTask<EmptyResult> Ping(RequestContext<PingRequestParams> request, CancellationToken cancellationToken)
    {
        return default;
    }

    public async ValueTask<EmptyResult> SetLoggingLevel(RequestContext<SetLevelRequestParams> request, CancellationToken cancellationToken)
    {
        if (request.Params is null)
        {
            throw new ArgumentNullException(nameof(request.Params), "SetLevelRequestParams is required.");
        }

        // 更新服务器上下文中的日志级别
        server.Context.LoggingLevel = request.Params.Level;

        return default;
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
