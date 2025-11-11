using DotNetCampus.ModelContextProtocol.Core;
using DotNetCampus.ModelContextProtocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol;

namespace DotNetCampus.ModelContextProtocol.Servers;

public class DefaultMcpServerHandlers(McpServer server)
{
    public async ValueTask<InitializeResult> Initialize(RequestContext<InitializeRequestParams> request, CancellationToken cancellationToken)
    {
        return new InitializeResult
        {
            ServerInfo = new ServerInfo
            {
                Name = "DotNetCampus.ModelContextProtocol",
                Version = "1.0.0",
            },
        };
    }

    public async ValueTask<ListToolsResult> ListTools(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
    {
        return new ListToolsResult
        {
            Tools = server.Tools.Select(x => x.Value.GetToolDefinition()).ToList(),
        };
    }

    public async ValueTask<NullResult> Ping(RequestContext<PingRequestParams> request, CancellationToken cancellationToken)
    {
        return default;
    }
}
