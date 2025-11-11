using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Core;
using DotNetCampus.ModelContextProtocol.Protocol;

namespace DotNetCampus.ModelContextProtocol.Servers;

public class DefaultMcpServerHandlers(McpServer server)
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
                Version = server.ServerVersion ?? "0.0.0",
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
            Tools = server.Tools.Select(x => x.Value.GetToolDefinition(InputSchemaJsonObjectJsonContext.Default)).ToList(),
        };
    }

    public async ValueTask<EmptyResult> Ping(RequestContext<PingRequestParams> request, CancellationToken cancellationToken)
    {
        return default;
    }
}
