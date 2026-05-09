# Transport

MCP 傳輸層只負責收發 JSON-RPC 訊息。業務程式碼通常只需要在伺服器端和用戶端選擇同一種傳輸層。

我們假設你在閱讀本文前，已經完成了 [快速開始](QuickStart.md) 中 MCP 伺服器和用戶端的建置。

> **核心原則**：`McpClientBuilder.Build()` 只建立用戶端物件，**不觸發任何 I/O 或網路連接**。連接在首次 API 呼叫時由 `EnsureConnectedAsync` 惰性觸發。詳細說明見 [用戶端"先建立後連接"原則](../knowledge/client-build-before-connect.md)。

## HTTP

[快速開始](QuickStart.md) 使用的就是 Streamable HTTP 傳輸層。

伺服器端：

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    // 监听 http://localhost:5943/mcp
    .WithLocalHostHttp(5943, "mcp")
    .Build();
```

用戶端：

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

本程式庫內建的 HTTP 伺服器端傳輸層只接聽 localhost。如果你需要接聽其他位址，可使用 `DotNetCampus.ModelContextProtocol.TouchSocket.Http` 擴充套件包提供的 TouchSocket HTTP 傳輸層。

## stdio

stdio 適合由用戶端啟動伺服器端處理序的情節，也是 MCP 協定建議伺服器支援的傳輸層。

伺服器端：

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // 通过标准输入输出收发 MCP 消息
    .WithStdio()
    .Build();

await mcpServer.RunAsync();
```

用戶端：

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

In-Process 適合同處理序嵌入和整合測試。伺服器端需要先啟動，用戶端第一次要求時會建立連接。

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

In-Process 傳輸層不提供處理序隔離，伺服器端與用戶端執行在同一處理序和權限下，適合可信邊界內的嵌入式情節和整合測試。

> `SampleTools` 的定義見 [Tools - 簡單範例](Tools.md#簡單範例)。

## IPC

IPC 傳輸層由 `DotNetCampus.ModelContextProtocol.Ipc` 套件提供，適合同一台機器上不同處理序之間通訊。

```bash
dotnet add package DotNetCampus.ModelContextProtocol.Ipc
```

伺服器端：

```csharp
var mcpServer = new McpServerBuilder("IPC 示例服务器", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // sample-mcp-pipe 是本地管道名，客户端需要使用同一个名字连接
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();

await mcpServer.RunAsync();
```

用戶端：

```csharp
await using var mcpClient = new McpClientBuilder("IPC 示例客户端", "1.0.0")
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();
```

## 自訂傳輸層

用戶端傳輸層實作 `IClientTransport`，並透過 `IClientTransportManager` 完成 JSON-RPC 序列化和回應分派。

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

註冊到用戶端：

```csharp
var mcpClient = new McpClientBuilder("自定义传输层客户端", "1.0.0")
    .WithTransport(manager => new MyClientTransport(manager))
    .Build();
```

伺服器端傳輸層實作 `IServerTransport`：

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

註冊到伺服器端：

```csharp
var mcpServer = new McpServerBuilder("自定义传输层服务器", "1.0.0")
    .WithTransport(manager => new MyServerTransport(manager))
    .Build();
```

如果你的伺服器端傳輸層需要支援伺服器主動向用戶端發起要求，例如 Sampling，還需要為每個用戶端連接實作 `IServerTransportSession`。

> **建構原則**：用戶端傳輸層建構函式**只儲存參數，不執行 I/O**。所有連接工作（啟動處理序、建立網路連接等）在 `ConnectAsync` 中完成，且 `ConnectAsync` 必須冪等。詳見 [用戶端"先建立後連接"原則](../knowledge/client-build-before-connect.md)。
