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
                        ListChanged = true,
                    }
                    : null,
                // 暂不支持 prompts 和 resources
                Prompts = null,
                Resources = null,
                // 暂不支持日志记录
                Logging = null,
            },
        };
    }

    public async ValueTask<ListToolsResult> ListTools(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
    {
        return new ListToolsResult
        {
            Tools = server.Tools.Select(x => x.Value.GetToolDefinition(InputSchemaJsonContext.Default)).ToList(),
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

        if (!server.Tools.TryGetValue(toolName, out var tool))
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = $"Tool '{toolName}' not found." }],
            };
        }

        try
        {
            var arguments = request.Params?.Arguments ?? default;
            var jsonSerializer = server.Context.JsonSerializer;
            var jsonContext = jsonSerializer switch
            {
                McpServerToolJsonSerializer mcpSerializer => mcpSerializer.JsonSerializerContext ?? InputSchemaJsonContext.Default,
                _ => InputSchemaJsonContext.Default,
            };

            return await tool.CallTool(arguments, jsonContext, cancellationToken);
        }
        catch (Exception ex)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = $"Error calling tool '{toolName}': {ex.Message}" }],
            };
        }
    }

    public async ValueTask<EmptyResult> Ping(RequestContext<PingRequestParams> request, CancellationToken cancellationToken)
    {
        return default;
    }
}
