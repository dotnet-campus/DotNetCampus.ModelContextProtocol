using DotNetCampus.ModelContextProtocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol;

namespace DotNetCampus.ModelContextProtocol.Servers;

public class DefaultMcpServerHandlers
{
    public async ValueTask<InitializeResult> Initialize(RequestContext<InitializeRequestParams> request, CancellationToken cancellationToken)
    {
        return new InitializeResult();
    }

    public async ValueTask<ListToolsResult> ListTools(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
    {
        return new ListToolsResult();
    }
}
