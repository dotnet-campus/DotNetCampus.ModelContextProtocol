using System.Diagnostics.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Core;
using DotNetCampus.ModelContextProtocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol;

namespace DotNetCampus.ModelContextProtocol.Servers;

public delegate ValueTask<TResult> McpRequestHandler<TParams, TResult>(
    RequestContext<TParams> request,
    CancellationToken cancellationToken);

public class McpServerHandlers(McpServer server)
{
    private readonly DefaultMcpServerHandlers _default = new(server);

    [NotNull]
    public McpRequestHandler<InitializeRequestParams, InitializeResult>? InitializeHandler
    {
        get => field ?? _default.Initialize;
        set;
    }

    [NotNull]
    public McpRequestHandler<PingRequestParams, EmptyResult>? PingHandler
    {
        get => field ?? _default.Ping;
        set;
    }

    [NotNull]
    public McpRequestHandler<ListToolsRequestParams, ListToolsResult>? ListToolsHandler
    {
        get => field ?? _default.ListTools;
        set;
    }

    // public McpRequestHandler<CallToolRequestParams, CallToolResult>? CallToolHandler { get; set; }
    //
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
    //
    // public McpRequestHandler<SetLevelRequestParams, EmptyResult>? SetLoggingLevelHandler { get; set; }

    public IEnumerable<KeyValuePair<string, Func<JsonRpcNotification, CancellationToken, ValueTask>>>? NotificationHandlers { get; set; }
}
