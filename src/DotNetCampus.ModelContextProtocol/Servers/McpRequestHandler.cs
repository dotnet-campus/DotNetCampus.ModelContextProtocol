using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol;

namespace DotNetCampus.ModelContextProtocol.Servers;

public delegate ValueTask<TResult> McpRequestHandler<TParams, TResult>(
    RequestContext<TParams> request,
    CancellationToken cancellationToken);

public record McpServerHandlers
{
    private readonly DefaultMcpServerHandlers _default = new();

    [NotNull]
    public McpRequestHandler<InitializeRequestParams, InitializeResult>? InitializeHandler
    {
        get => field ?? _default.Initialize;
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

    public async Task HandleRequest(JsonRpcRequest request, StreamWriter writer, CancellationToken cancellationToken = default)
    {
        if (request.Method == "initialize")
        {
            if (request.Params is not JsonElement paramsElement)
            {
                throw new NotSupportedException($"暂未支持无参数的 {request.Method} 请求");
            }

            var initializeRequestParams = paramsElement.Deserialize(McpServerRequestJsonContext.Default.InitializeRequestParams);
            var requestContext = new RequestContext<InitializeRequestParams>(initializeRequestParams);
            var result = await InitializeHandler(requestContext, cancellationToken);
            var response = new JsonRpcResponse
            {
                Id = request.Id,
                Result = JsonSerializer.SerializeToElement(result, McpServerResponseJsonContext.Default.InitializeResult),
            };
            await writer.WriteAsync($"event: message\n");
            var responseText = JsonSerializer.Serialize(response, McpServerResponseJsonContext.Default.JsonRpcResponse);
            await writer.WriteAsync($"data: {responseText}\n\n");
        }
    }
}
