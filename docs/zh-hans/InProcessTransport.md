# In-Process 传输层使用指南

## 使用方式

```csharp
var mcpServer = new McpServerBuilder("内嵌服务器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    // WithInProcess 同时创建连接对，后续传给客户端
    .WithInProcess(out var transportPair)
    .Build();

await mcpServer.StartAsync();

var mcpClient = new McpClientBuilder()
    .WithInProcess(transportPair)
    .Build();

var tools = await mcpClient.ListToolsAsync();
```

每个 `InProcessTransportPair` 是一对一连接，一个连接对只能绑定一个服务端和一个客户端。如果需要多个并发客户端，在服务器上多次调用 `WithInProcess` 即可：

```csharp
var mcpServer = new McpServerBuilder("内嵌服务器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithInProcess(out var pair1)
    .WithInProcess(out var pair2)
    .Build();

await mcpServer.StartAsync();

var client1 = new McpClientBuilder().WithInProcess(pair1).Build();
var client2 = new McpClientBuilder().WithInProcess(pair2).Build();
```

In-Process 传输层同样支持 Sampling，详见 [Sampling 使用指南](Sampling.md)。

自定义类型的参数和返回值同样需要通过 `WithJsonSerializer` 注册序列化上下文（In-Process 传输层仍然使用 JSON-RPC 文本格式，不绕过序列化）：

```csharp
var mcpServer = new McpServerBuilder("内嵌服务器", "1.0.0")
    .WithJsonSerializer(MyToolJsonContext.Default)   // 自定义类型必须注册
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithInProcess(out var transportPair)
    .Build();
```

In-Process 传输层不提供进程隔离，服务端与客户端运行在同一进程和权限下，仅适用于同一信任边界内的嵌入式或测试场景。
