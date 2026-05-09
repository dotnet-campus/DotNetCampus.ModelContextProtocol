# 客户端"先创建后连接"原则

> 本文档描述 MCP 客户端的生命周期设计原则：`McpClientBuilder.Build()` 只创建客户端实例，不触发实际连接。连接在首次 API 调用时由 `EnsureConnectedAsync` 惰性触发。

## 原则

**所有传输层**（Stdio、HTTP、InProcess、IPC 等）都必须遵循同一生命周期：

```
McpClientBuilder.Build()  →  仅创建对象、保存配置
McpClient.XXXAsync()      →  首次调用时触发 EnsureConnectedAsync → ConnectAsync
```

即：`Build()` 阶段 **禁止** 执行任何 I/O 操作、启动进程、建立网络连接或建立传输管道。

## 好处

### 1. 全异步请求模式

当一个应用程序需要连接多个 MCP 服务器时，可以在启动阶段一次性创建所有客户端，然后在实际使用时才按需连接：

```csharp
// 启动阶段：快速创建所有客户端（无 I/O）
var clientA = new McpClientBuilder().WithHttp("https://server-a/mcp").Build();
var clientB = new McpClientBuilder().WithStdio("server-b").Build();
var clientC = new McpClientBuilder().WithInProcess(mcpServer).Build();

// 使用阶段：按需连接、并发请求
var taskA = clientA.CallToolAsync("tool-a", argsA);
var taskB = clientB.CallToolAsync("tool-b", argsB);
var taskC = clientC.CallToolAsync("tool-c", argsC);
await Task.WhenAll(taskA, taskB, taskC);
```

如果 `Build()` 必须等待连接完成，上述代码就无法实现全异步——在发起请求之前连 `McpClient` 实例都没有。

### 2. InProcess 传输层可在服务器启动前创建客户端

```csharp
var server = new McpServerBuilder("Server", "1.0.0")
    .WithInProcess()
    .WithTools(t => t.WithTool(() => new MyTool()))
    .Build();

// 在服务器启动前就创建客户端。
var client = new McpClientBuilder()
    .WithInProcess(server)
    .Build();

// 稍后启动服务器。
await server.StartAsync();

// 客户端首次 API 调用时才连接。
var result = await client.CallToolAsync("my_tool", args);
```

### 3. 简化依赖注入与对象组装

客户端可以在 DI 容器中注册为单例或作用域服务，而不需要在注册时就等待异步连接完成。

## 副作用

1. **首次调用延迟**：第一次 API 调用会比后续调用慢，因为需要建立连接和完成 MCP 协议初始化握手。
2. **连接错误延迟暴露**：如果服务器地址错误、进程启动失败等，错误不会在 `Build()` 时抛出，而是在首次 API 调用时才抛出。
3. **状态不确定性**：`Build()` 返回的 `McpClient` 的 `IsConnected` 为 `false`，直到首次成功调用后才变为 `true`。

为降低副作用 2 的影响，`McpClient` 提供了公开的 `EnsureConnectedAsync` 方法。开发者可以在会话开始前主动调用此方法，将不可用的服务提前过滤掉，而不是等到业务请求时才发现异常。

`EnsureConnectedAsync` 是幂等的，多次调用不会重复连接。所有 API 方法（如 `CallToolAsync`）内部也会自动调用此方法，因此不显式调用也完全正常。

## 对传输层开发者的要求

实现新的 `IClientTransport` 时，必须遵循：

1. **构造函数只保存参数**：构造函数（以及 `McpClientBuilder.WithTransport` 的工厂委托）中不得执行 I/O、启动进程或建立连接。
2. **`ConnectAsync` 承担所有连接工作**：包括启动进程、建立网络连接、建立管道等。
3. **`ConnectAsync` 幂等**：多次调用 `ConnectAsync` 应当安全，只有首次调用执行实际连接。

### 各传输层实现参考

| 传输层    | 构造阶段                   | ConnectAsync 阶段           |
|-----------|----------------------------|-----------------------------|
| Stdio     | 保存命令行参数             | 启动子进程、开始读写 stdin/stdout |
| HTTP      | 保存 ServerUrl             | 标记状态（真正连接在首次 POST 时建立） |
| InProcess | 保存 `InProcessServerTransport` 引用 | 调用 `transport.Connect()` 建立内存管道、启动消息循环 |
| IPC       | 保存管道名称和配置         | 创建 `IpcProvider`、连接到服务器管道 |

## 测试覆盖

以下测试确保此行为被固定：

- `BuildBeforeConnect_CanCallAfterServerStarts`：先创建客户端，后启动服务器，验证调用成功。
- `BuildBeforeConnect_MultipleClientsCanCallAfterServerStarts`：多个客户端先创建，服务器启动后并发调用。
- `Connect_ThrowsWhenServerNotStarted`：服务器始终未启动，客户端首次 API 调用抛出异常。
