# Transport

MCP 传输层只负责收发 JSON-RPC 消息。业务代码通常只需要在服务端和客户端选择同一种传输层。

## 速览

| 传输层           | 核心库内置 | 监听地址            | 适用场景             |
| ---------------- | ---------- | ------------------- | -------------------- |
| HTTP（内置）     | ✅          | 仅限 `localhost`    | 本地开发、单机部署   |
| TouchSocket HTTP | ❌ 需扩展   | 可监听 `0.0.0.0` 等 | 局域网/公网部署      |
| stdio            | ✅          | -                   | 客户端启动服务端进程 |
| In-Process       | ✅          | -                   | 同进程嵌入、集成测试 |
| IPC              | ❌ 需扩展   | -                   | 同机跨进程高速通信   |

> 核心库内置的 HTTP 传输层仅监听本机回环地址，是出于安全和尽可能少引入依赖的考虑。如果需要监听 `0.0.0.0` 等非回环地址，请使用 [TouchSocket HTTP](#touchsocket-http扩展)；如果需要 IPC 传输层，请使用 [IPC](#ipc)。这两种传输层的具体获取方式见 [扩展传输层的两种获取方式](#扩展传输层的两种获取方式)。

---

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

> 本库内置的 HTTP 服务端传输层（`LocalHostHttpServerTransport`）仅监听 `127.0.0.1` 和 `[::1]`。如果你需要监听其他地址（如 `0.0.0.0`），请参考下方 [TouchSocket HTTP（扩展）](#touchsocket-http扩展) 章节。

---

## TouchSocket HTTP（扩展）

核心库内置的 HTTP 传输层仅监听本机回环地址。如果你需要监听 `0.0.0.0` 等非回环地址（例如部署到局域网或公网），可以使用 TouchSocket HTTP 传输层。

TouchSocket HTTP 传输层有两种获取方式，详见 [扩展传输层的两种获取方式](#扩展传输层的两种获取方式)。以下示例假设你已通过任一方式获得 TouchSocket HTTP 传输层支持：

### 服务端

```csharp
// 简单重载：监听本地 localhost
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithTouchSocketHttp(5943, "mcp")
    .Build();

// 监听 0.0.0.0（所有网络接口，包括局域网和公网）
var mcpServer = new McpServerBuilder("公网服务器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithTouchSocketHttp(["0.0.0.0:5943", "[::]:5943"], "mcp")
    .Build();

// 选项重载：可配置全部参数
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithTouchSocketHttp(new TouchSocketHttpServerTransportOptions
    {
        Listen = ["0.0.0.0:5943", "[::]:5943"],
        EndPoint = "mcp",
    })
    .Build();
```

`Listen` 列表使用 `"IP:端口"` 格式，只能使用 IP 地址，不能使用域名。可同时监听多个地址和端口。

### 复用已有的 HttpService

如果你已经有正在运行的 `HttpService`（TouchSocket 的核心类型），可以将 MCP 服务端作为插件挂载上去：

```csharp
// httpService 是你已有的 HttpService 实例
httpService.UseMcpServer("示例服务器", "1.0.0", builder =>
{
    builder.WithTools(t => t.WithTool(() => new SampleTools()));
});

// 也可以指定自定义端点
httpService.UseMcpServer("示例服务器", "1.0.0", "/custom-mcp", builder =>
{
    builder.WithTools(t => t.WithTool(() => new SampleTools()));
});
```

> `HttpService` 实现了 `IPluginManager` 接口，`UseMcpServer` 是 `IPluginManager` 的扩展方法。

### 客户端

TouchSocket HTTP 传输层的客户端无需特殊处理——客户端只需向服务端发起 HTTP 请求，可直接复用核心库的 HTTP 客户端传输层：

```csharp
var mcpClient = new McpClientBuilder("示例客户端", "1.0.0")
    .WithHttp("http://192.168.1.100:5943/mcp")
    .Build();
```

---

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

IPC 传输层适合同一台机器上不同进程之间通信，基于 dotnetCampus.Ipc 提供的命名管道实现。

IPC 传输层有两种获取方式，详见 [扩展传输层的两种获取方式](#扩展传输层的两种获取方式)。以下示例假设你已通过任一方式获得 IPC 传输层支持：

### 服务端

```csharp
var mcpServer = new McpServerBuilder("IPC 示例服务器", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // sample-mcp-pipe 是本地管道名，客户端需要使用同一个名字连接
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();

await mcpServer.RunAsync();
```

也可以复用外部创建的 `IpcProvider`：

```csharp
var mcpServer = new McpServerBuilder("IPC 示例服务器", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    .WithDotNetCampusIpc(existingIpcProvider)
    .Build();
```

### 客户端

```csharp
await using var mcpClient = new McpClientBuilder("IPC 示例客户端", "1.0.0")
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();
```

也可以复用外部创建的 `IpcProvider`：

```csharp
await using var mcpClient = new McpClientBuilder("IPC 示例客户端", "1.0.0")
    .WithDotNetCampusIpc(existingIpcProvider, "sample-mcp-pipe")
    .Build();
```

---

## 扩展传输层的两种获取方式

核心库仅内置了 HTTP（localhost）、stdio 和 In-Process 三种传输层。如果需要 TouchSocket HTTP 或 IPC 传输层，可以通过以下两种方式获取：

### 方式一：安装扩展包（推荐）

直接安装对应的扩展 NuGet 包：

```bash
# TouchSocket HTTP 传输层
dotnet add package DotNetCampus.ModelContextProtocol.TouchSocket.Http

# IPC 传输层
dotnet add package DotNetCampus.ModelContextProtocol.Ipc
```

安装后即可直接使用 `.WithTouchSocketHttp()` / `.WithDotNetCampusIpc()` 等扩展方法。扩展包已将底层依赖（`TouchSocket.Http` / `dotnetCampus.Ipc`）一并引入，这是最简单的方式。

### 方式二：自行安装底层库 + 启用源生成器

如果你希望尽可能减少项目中引入的 `.dll` 文件数量（我就喜欢这么干），可以不安装扩展包，而是自行安装底层库，并启用源生成器自动生成传输层代码：

```bash
# 安装底层库（而非扩展包）
dotnet add package dotnetCampus.Ipc
dotnet add package TouchSocket.Http
```

然后在项目 `.csproj` 中开启源生成器：

```xml
<PropertyGroup>
  <DotNetCampusModelContextProtocolGenerateTransports>true</DotNetCampusModelContextProtocolGenerateTransports>
</PropertyGroup>
```

开启此选项后，`DotNetCampus.ModelContextProtocol` 核心包附带的分析器（Analyzer）会自动扫描项目中已安装的库：

| 检测到的库         | 自动生成的传输层        |
| ------------------ | ----------------------- |
| `dotnetCampus.Ipc` | IPC 传输层              |
| `TouchSocket.Http` | TouchSocket HTTP 传输层 |

**未安装的库不会生成任何代码**——源生成器是安全的，不会污染项目。

> **设计理念**：dotnet-campus 组织倾向于保持核心库的零依赖和轻量化。IPC 和 TouchSocket HTTP 作为可选的扩展传输层，不会被强行塞入核心库。开发者可以根据实际需要自取所需的传输层，而不会被迫引入不需要的依赖。

---

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
