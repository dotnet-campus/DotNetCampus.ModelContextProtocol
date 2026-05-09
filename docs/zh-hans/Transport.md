# Transport

MCP 传输层只负责收发 JSON-RPC 消息。业务代码通常只需要在服务端和客户端选择同一种传输层。

## HTTP

[服务端快速开始](Server_QuickStart.md) 和 [客户端快速开始](Client_QuickStart.md) 使用的就是 HTTP 传输层。服务端调用 `WithLocalHostHttp(5943, "mcp")`，客户端调用 `WithHttp("http://localhost:5943/mcp")`。

## stdio

stdio 适合由客户端启动服务端进程的场景。

服务端：

```csharp
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Servers;

var mcpServer = new McpServerBuilder("StdioServer", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new CalculatorTools()))
    .WithStdio()
    .Build();

await mcpServer.RunAsync();

public class CalculatorTools
{
    [McpServerTool(ReadOnly = true)]
    public int Add(int a, int b) => a + b;
}
```

客户端：

```csharp
using DotNetCampus.ModelContextProtocol.Clients;

await using var mcpClient = new McpClientBuilder()
    .WithClientInfo("StdioClient", "1.0.0")
    .WithStdio("dotnet", ["run", "--project", "../MinimalMcpServer"])
    .Build();
```

## In-Process

In-Process 适合同进程嵌入和集成测试。服务端需要先启动，客户端第一次请求时会建立连接。

```csharp
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

var mcpServer = new McpServerBuilder("InProcessServer", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new CalculatorTools()))
    .WithInProcess()
    .Build();

await mcpServer.StartAsync();
try
{
    await using var mcpClient = new McpClientBuilder()
        .WithClientInfo("InProcessClient", "1.0.0")
        .WithInProcess(mcpServer)
        .Build();

    var arguments = JsonSerializer.SerializeToElement(new { a = 1, b = 2 });
    var result = await mcpClient.CallToolAsync("add", arguments);

    Console.WriteLine(result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text);
}
finally
{
    await mcpServer.StopAsync();
}

public class CalculatorTools
{
    [McpServerTool(ReadOnly = true)]
    public int Add(int a, int b) => a + b;
}
```

## IPC

IPC 传输层由 `DotNetCampus.ModelContextProtocol.Ipc` 包提供，适合同一台机器上不同进程之间通信。

```bash
dotnet add package DotNetCampus.ModelContextProtocol.Ipc
```

服务端：

```csharp
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Servers;

var mcpServer = new McpServerBuilder("IpcServer", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new CalculatorTools()))
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();

await mcpServer.RunAsync();

public class CalculatorTools
{
    [McpServerTool(ReadOnly = true)]
    public int Add(int a, int b) => a + b;
}
```

客户端：

```csharp
using DotNetCampus.ModelContextProtocol.Clients;

await using var mcpClient = new McpClientBuilder()
    .WithClientInfo("IpcClient", "1.0.0")
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();
```

## 自定义传输层

客户端传输层实现 `IClientTransport`，并通过 `IClientTransportManager` 完成 JSON-RPC 序列化和响应分发。

```csharp
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Transports;

await using var mcpClient = new McpClientBuilder()
    .WithClientInfo("CustomClient", "1.0.0")
    .WithTransport(manager => new MyClientTransport(manager))
    .Build();

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
        using var stream = new MemoryStream();
        await manager.WriteMessageAsync(stream, message, cancellationToken);
        await SendToConnectionAsync(stream.ToArray(), cancellationToken);
    }

    public ValueTask DisposeAsync() => DisconnectAsync();

    private static ValueTask SendToConnectionAsync(byte[] payload, CancellationToken cancellationToken)
    {
        // 把 payload 写入你的底层通道。
        return ValueTask.CompletedTask;
    }
}
```

服务端传输层实现 `IServerTransport`。收到请求后，用 `IServerTransportManager.ReadMessageAsync` 解析消息，用 `HandleRequestAsync` 交给 MCP 服务端处理，再用 `WriteMessageAsync` 写回响应。若传输层支持服务器主动请求客户端，还需要为每个连接实现 `IServerTransportSession`。
