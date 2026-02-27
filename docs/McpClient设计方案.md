# McpClient 设计方案

现有的 `McpServer` 的初始化代码如下：

```csharp
var mcpServer = new McpServerBuilder("SampleMcpServer", "1.0.0")
    .WithLogger(new McpLoggerBridge(Log.Current))
    .WithLocalHostHttp(new LocalHostHttpTransportOptions
    {
        Port = 5943,
        EndPoint = "mcp",
        IsCompatibleWithSse = true,
    })
    // .WithStdio()
    .WithJsonSerializer(McpToolJsonContext.Default)
    .WithTools(t => t
        .WithTool(() => new SampleTool())
        .WithTool(() => new InputTool())
        .WithTool(() => new OutputTool())
        .WithTool(() => new PolymorphicTool())
        .WithTool(() => new ResourceTool())
    )
    .WithResources(r => r
        .WithResource(() => new SampleResource())
    )
    .Build();
mcpServer.EnableDebugMode();
await mcpServer.RunAsync();
```

那么，预期的 `McpClient` 的初始化代码设计方案如下：

```csharp
var mcpClient = new McpClientBuilder()
    .WithLogger(new McpLoggerBridge(Log.Current))
    .WithHttp(new HttpClientTransportOptions
    {
        EndPoint = "http://localhost:5943/mcp",
    })
    // .WithStdio(new StdioClientTransportOptions
    // {
    //     Command = "dnx",
    //     Arguments = ["xxx"],
    // })
    .Build();
```

连接方案有三：

1. `McpClient` 是已连接的对象，`Build` 方法改为 `BuildAsync`，负责连接。这也是微软官方采用的方案。
1. `McpClient` 可以是未连接的对象，不会自动连接，但在每个与服务器通信的方法（如 `ListToolsAsync`）调用前都会确保连接或重连。
1. `Build` 出来的是 `McpClientInfo`，不负责连接，也没有与服务器通信的方法，调用 `ConnectAsync` 方法后返回 `McpClient` 对象，负责与服务器通信。

我们选方案二。

注：

- MCP 官方协议要求 `McpClient` 与 `McpServer` 是一对一对应关系
- MCP 官方协议要求 MCP 主机（`McpHost`）负责管理多个 `McpClient`
