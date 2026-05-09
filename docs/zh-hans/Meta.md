# _meta

MCP 协议的 [`_meta`](https://modelcontextprotocol.io/specification/2025-11-25/basic#_meta) 字段允许在 MCP 请求中携带额外的元数据。一个典型用途是**分布式追踪**：客户端将 TraceId 注入 `_meta`，服务端提取后进行埋点。

## 客户端：注入 TraceId

客户端继承 `McpClientRequestHandlers`，通过重写 `OnRequestSending` 全局钩子，将当前 `Activity.Current` 的 TraceId 注入 `_meta`：

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

> **提示**：也可选择重写 `CallToolAsync`、`ListToolsAsync` 等方法逐个注入 `_meta`。
> 相比之下，`OnRequestSending` 一处重写即可覆盖所有请求类型，是推荐做法。

注册到客户端：

```csharp
var mcpClient = new McpClientBuilder("示例客户端", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .WithRequestHandlers<TracingClientRequestHandlers>(client => new TracingClientRequestHandlers(client))
    .Build();
```

## 服务端：提取 TraceId 并创建 Activity

服务端通过 `McpServerRequestHandlers` 拦截请求，从 `_meta` 提取 TraceId 并创建子 Activity 用于埋点：

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

注册到服务端：

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithLocalHostHttp(5943, "mcp")
    .WithRequestHandlers<TracingServerRequestHandlers>(s => new TracingServerRequestHandlers(s))
    .Build();
```

除了通过 `McpServerRequestHandlers` 全局拦截，也可以在单个工具或资源方法中直接读取 `Context.Meta`：

```csharp
// 在工具方法中
[McpServerTool]
public string EchoWithTrace(IMcpServerCallToolContext context, string text)
{
    if (context.Meta.TryGetProperty("traceparent", out var tp))
    {
        Console.WriteLine($"TraceId: {tp.GetString()}");
    }
    return text;
}

// 在资源方法中
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

这种方式适合只需要在个别方法中读取 `_meta` 的场景。

## 关于 _meta 与分布式追踪

- MCP 协议不定义标准的 Trace Context 传播机制，但 OpenTelemetry 社区推荐通过 `params._meta` 传递 `traceparent` 和 `tracestate`
- 这一约定正在 MCP 社区讨论中，未来可能会有官方规范（[modelcontextprotocol#246](https://github.com/modelcontextprotocol/modelcontextprotocol/issues/246)、[modelcontextprotocol#414](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/414)）
- 在 MCP 标准尚未达成前，通过本库的 `WithRequestHandlers` 可以自行实现 TraceContext 注入和提取
