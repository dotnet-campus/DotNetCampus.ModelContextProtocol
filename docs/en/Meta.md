# _meta

The [`_meta`](https://modelcontextprotocol.io/specification/2025-11-25/basic#_meta) field in the MCP protocol allows carrying additional metadata in MCP requests. A typical use case is **distributed tracing**: the client injects a TraceId into `_meta`, and the server extracts it for instrumentation.

## Client: Inject TraceId

The client inherits `McpClientRequestHandlers` and overrides the `OnRequestSending` global hook to inject the TraceId of the current `Activity.Current` into `_meta`:

```csharp
public sealed class TracingClientRequestHandlers(McpClient client) : McpClientRequestHandlers(client)
{
    protected internal override void OnRequestSending(RequestParams requestParams, string method)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        var meta = new Dictionary<string, string>
        {
            ["traceparent"] = activity.Id,
        };
        if (!string.IsNullOrEmpty(activity.TraceStateString))
        {
            meta["tracestate"] = activity.TraceStateString;
        }

        requestParams.Meta = JsonSerializer.SerializeToElement(meta);
    }
}
```

> **Tip**: You can also override individual methods like `CallToolAsync` or `ListToolsAsync` to inject `_meta` per-request.
> In contrast, overriding `OnRequestSending` once covers all request types and is the recommended approach.

Register with the client:

```csharp
var mcpClient = new McpClientBuilder("Sample Client", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .WithRequestHandlers<TracingClientRequestHandlers>(client => new TracingClientRequestHandlers(client))
    .Build();
```

## Server: Extract TraceId and Create Activity

The server intercepts requests via `McpServerRequestHandlers`, extracts the TraceId from `_meta`, and creates a child Activity for instrumentation:

```csharp
public sealed class TracingServerRequestHandlers(McpServer server) : McpServerRequestHandlers(server)
{
    public override async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> rawRequest,
        string? toolName, IMcpServerTool? tool, IMcpServerCallToolContext? context)
    {
        var meta = rawRequest.Params?.Meta;
        var parentId = meta?.TryGetProperty("traceparent", out var tpElement) == true
            ? tpElement.GetString()
            : null;

        using var activity = parentId is not null
            ? ActivitySource.StartActivity("tools/call", ActivityKind.Server, parentId)
            : null;

        activity?.SetTag("mcp.method.name", "tools/call");
        activity?.SetTag("gen_ai.tool.name", toolName);

        var result = await base.CallToolAsync(rawRequest, toolName, tool, context);

        if (result.RawException is { } ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }

        return result;
    }
}
```

Register with the server:

```csharp
var mcpServer = new McpServerBuilder("Sample Server", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithLocalHostHttp(5943, "mcp")
    .WithRequestHandlers<TracingServerRequestHandlers>(s => new TracingServerRequestHandlers(s))
    .Build();
```

In addition to global interception via `McpServerRequestHandlers`, you can also read `Context.Meta` directly in individual tool or resource methods:

```csharp
// In a tool method
[McpServerTool]
public string EchoWithTrace(IMcpServerCallToolContext context, string text)
{
    if (context.Meta.TryGetProperty("traceparent", out var tp))
    {
        Console.WriteLine($"TraceId: {tp.GetString()}");
    }
    return text;
}

// In a resource method
[McpServerResource(UriTemplate = "sample://status", Name = "Server Status")]
public string GetStatus(IMcpServerReadResourceContext context)
{
    if (context.Meta.TryGetProperty("traceparent", out var tp))
    {
        Console.WriteLine($"TraceId: {tp.GetString()}");
    }
    return "OK";
}
```

This approach is suitable for scenarios where you only need to read `_meta` in a few individual methods.

## _meta and Distributed Tracing

- The MCP protocol does not define a standard Trace Context propagation mechanism, but the OpenTelemetry community recommends passing `traceparent` and `tracestate` through `params._meta`
- This convention is under discussion in the MCP community and may become an official specification in the future ([modelcontextprotocol#246](https://github.com/modelcontextprotocol/modelcontextprotocol/issues/246), [modelcontextprotocol#414](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/414))
- Until an MCP standard is established, you can implement TraceContext injection and extraction yourself using this library's `WithRequestHandlers`
