# Transport

MCP 传输层只负责收发 JSON-RPC 消息。业务代码通常只需要在服务端和客户端选择同一种传输层。

我们假设你在阅读本文前，已经完成了 [快速开始](QuickStart.md) 中 MCP 服务器和客户端的搭建。

> **核心原则**：`McpClientBuilder.Build()` 只创建客户端对象，**不触发任何 I/O 或网络连接**。连接在首次 API 调用时由 `EnsureConnectedAsync` 惰性触发。详细说明见 [客户端"先创建后连接"原则](../knowledge/client-build-before-connect.md)。

## HTTP

[快速开始](QuickStart.md) 使用的就是 Streamable HTTP 传输层。

服务端：

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    // 监听 http://localhost:5943/mcp
    .WithLocalHostHttp(5943, "mcp")
    .Build();
```

客户端：

```csharp
// 简单重载：仅指定 URL
var mcpClient = new McpClientBuilder("示例客户端", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

// 选项重载：可配置自定义 HttpClient（认证头、代理等）
var mcpClient = new McpClientBuilder("示例客户端", "1.0.0")
    .WithHttp(new HttpClientTransportOptions
    {
        ServerUrl = "http://localhost:5943/mcp",
        HttpClient = customHttpClient,
    })
    .Build();
```

本库内置的 HTTP 服务端传输层只监听 localhost。如果你需要监听其他地址，可使用 `DotNetCampus.ModelContextProtocol.TouchSocket.Http` 扩展包提供的 TouchSocket HTTP 传输层。

## stdio

stdio 适合由客户端启动服务端进程的场景，也是 MCP 协议建议服务器支持的传输层。

服务端：

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // 通过标准输入输出收发 MCP 消息
    .WithStdio()
    .Build();

await mcpServer.RunAsync();
```

客户端：

```csharp
// 简单重载：指定命令和参数
var mcpClient = new McpClientBuilder("示例客户端", "1.0.0")
    // 客户端会启动此命令，并通过该进程的标准输入输出通信
    .WithStdio("dotnet", ["run", "--project", "../MinimalMcpServer"])
    .Build();

// 选项重载：可配置环境变量
var mcpClient = new McpClientBuilder("示例客户端", "1.0.0")
    .WithStdio(new StdioClientTransportOptions
    {
        Command = "dotnet",
        Arguments = ["run", "--project", "../MinimalMcpServer"],
        EnvironmentVariables = new Dictionary<string, string>
        {
            ["DOTNET_ENVIRONMENT"] = "Production",
        },
    })
    .Build();
```

## In-Process

In-Process 适合同进程嵌入和集成测试。服务端需要先启动，客户端第一次请求时会建立连接。

```csharp
var mcpServer = new McpServerBuilder("内嵌服务器", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // 允许同进程内的 MCP 客户端连接
    .WithInProcess()
    .Build();

await mcpServer.StartAsync();
try
{
    await using var mcpClient = new McpClientBuilder("内嵌客户端", "1.0.0")
        .WithInProcess(mcpServer)
        .Build();

    var arguments = JsonSerializer.SerializeToElement(new { text = "Hello" });
    var result = await mcpClient.CallToolAsync("echo_tool", arguments);

    Console.WriteLine(result.Content);
}
finally
{
    await mcpServer.StopAsync();
}
```

In-Process 传输层不提供进程隔离，服务端与客户端运行在同一进程和权限下，适合可信边界内的嵌入式场景和集成测试。

> `SampleTools` 的定义见 [Tools - 简单示例](Tools.md#简单示例)。

## IPC

IPC 传输层由 `DotNetCampus.ModelContextProtocol.Ipc` 包提供，适合同一台机器上不同进程之间通信。

```bash
dotnet add package DotNetCampus.ModelContextProtocol.Ipc
```

服务端：

```csharp
var mcpServer = new McpServerBuilder("IPC 示例服务器", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // sample-mcp-pipe 是本地管道名，客户端需要使用同一个名字连接
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();

await mcpServer.RunAsync();
```

客户端：

```csharp
await using var mcpClient = new McpClientBuilder("IPC 示例客户端", "1.0.0")
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();
```

## 自定义传输层

客户端传输层实现 `IClientTransport`，并通过 `IClientTransportManager` 完成 JSON-RPC 序列化和响应分发。

```csharp
public sealed class MyClientTransport(IClientTransportManager manager) : IClientTransport
{
    public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        // 在这里连接到你的底层通道。
        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        // 在这里关闭底层通道。
        return ValueTask.CompletedTask;
    }

    public async ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        // 将 JSON-RPC 消息序列化成字符串，然后发送到底层通道。
        var line = manager.WriteMessageAsync(message);
        await SendLineAsync(line, cancellationToken);
    }

    public ValueTask DisposeAsync() => DisconnectAsync();

    private async Task OnLineReceivedAsync(string line, CancellationToken cancellationToken)
    {
        // 底层通道收到服务端消息后，反序列化并交回 MCP 客户端处理。
        var message = await manager.ReadMessageAsync(line);
        switch (message)
        {
            case JsonRpcResponse response:
                await manager.HandleRespondAsync(response, cancellationToken);
                break;

            case JsonRpcRequest request:
                await manager.HandleServerRequestAsync(request, cancellationToken);
                break;
        }
    }

    private static ValueTask SendLineAsync(string line, CancellationToken cancellationToken)
    {
        // 把 line 写入你的底层通道。
        return ValueTask.CompletedTask;
    }
}
```

注册到客户端：

```csharp
var mcpClient = new McpClientBuilder("自定义传输层客户端", "1.0.0")
    .WithTransport(manager => new MyClientTransport(manager))
    .Build();
```

服务端传输层实现 `IServerTransport`：

```csharp
public sealed class MyServerTransport(IServerTransportManager manager) : IServerTransport
{
    public Task<Task> StartAsync(CancellationToken startingCancellationToken, CancellationToken runningCancellationToken)
    {
        // 第一层 Task 完成表示传输层已启动，返回的第二层 Task 在传输层停止时完成。
        return Task.FromResult(RunAsync(runningCancellationToken));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task OnLineReceivedAsync(string line, Stream responseStream, CancellationToken cancellationToken)
    {
        var message = await manager.ReadMessageAsync(line);
        if (message is not JsonRpcRequest request)
        {
            return;
        }

        // 将客户端请求交给 MCP 服务端处理。
        var response = await manager.HandleRequestAsync(request, cancellationToken: cancellationToken);
        if (response is not null)
        {
            await manager.WriteMessageAsync(responseStream, response, cancellationToken);
        }
    }

    private static async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
```

注册到服务端：

```csharp
var mcpServer = new McpServerBuilder("自定义传输层服务器", "1.0.0")
    .WithTransport(manager => new MyServerTransport(manager))
    .Build();
```

如果你的服务端传输层需要支持服务器主动向客户端发起请求，例如 Sampling，还需要为每个客户端连接实现 `IServerTransportSession`。

> **构造原则**：客户端传输层构造函数**只保存参数，不执行 I/O**。所有连接工作（启动进程、建立网络连接等）在 `ConnectAsync` 中完成，且 `ConnectAsync` 必须幂等。详见 [客户端"先创建后连接"原则](../knowledge/client-build-before-connect.md)。
