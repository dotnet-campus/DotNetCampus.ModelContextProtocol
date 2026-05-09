# _meta

MCP 協定的 [`_meta`](https://modelcontextprotocol.io/specification/2025-11-25/basic#_meta) 欄位允許在 MCP 請求中攜帶額外的中繼資料。一個典型用途是**分散式追蹤**：用戶端將 TraceId 注入 `_meta`，伺服端提取後進行埋點。

## 用戶端：注入 TraceId

用戶端繼承 `McpClientRequestHandlers`，透過重寫 `OnRequestSending` 全域掛勾，將當前 `Activity.Current` 的 TraceId 注入 `_meta`：

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

> **提示**：也可選擇重寫 `CallToolAsync`、`ListToolsAsync` 等方法逐個注入 `_meta`。
> 相比之下，`OnRequestSending` 一處重寫即可涵蓋所有請求類型，是推薦做法。

註冊到用戶端：

```csharp
var mcpClient = new McpClientBuilder("示例客戶端", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .WithRequestHandlers<TracingClientRequestHandlers>(client => new TracingClientRequestHandlers(client))
    .Build();
```

## 伺服端：提取 TraceId 並建立 Activity

伺服端透過 `McpServerRequestHandlers` 攔截請求，從 `_meta` 提取 TraceId 並建立子 Activity 用於埋點：

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

註冊到伺服端：

```csharp
var mcpServer = new McpServerBuilder("示例伺服器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithLocalHostHttp(5943, "mcp")
    .WithRequestHandlers<TracingServerRequestHandlers>(s => new TracingServerRequestHandlers(s))
    .Build();
```

除了透過 `McpServerRequestHandlers` 全域攔截，也可以在單個工具或資源方法中直接讀取 `Context.Meta`：

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

// 在資源方法中
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

這種方式適合只需要在個別方法中讀取 `_meta` 的場景。

## 關於 _meta 與分散式追蹤

- MCP 協定不定義標準的 Trace Context 傳播機制，但 OpenTelemetry 社群推薦透過 `params._meta` 傳遞 `traceparent` 和 `tracestate`
- 這一約定正在 MCP 社群討論中，未來可能會有官方規範（[modelcontextprotocol#246](https://github.com/modelcontextprotocol/modelcontextprotocol/issues/246)、[modelcontextprotocol#414](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/414)）
- 在 MCP 標準尚未達成前，透過本庫的 `WithRequestHandlers` 可以自行實作 TraceContext 注入和提取
