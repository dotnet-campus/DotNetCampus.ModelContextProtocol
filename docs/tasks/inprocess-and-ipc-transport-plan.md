# InProcess 与 IPC 传输层改进计划

> 目标：解决 InProcess 和 IPC 传输层的三个核心问题，使其完整支持一服务器对多客户端通信。

## 问题概述

| # | 问题 | 影响范围 |
|---|------|---------|
| 1 | InProcess 传输层只支持一对一通信 | `InProcessTransportPair` 通过 `Interlocked.CompareExchange` 强制限制只能绑定一个客户端和一个服务端 |
| 2 | IPC 传输层只支持一对一通信 | 实际上 IPC 服务端已有 `ConcurrentDictionary<string, IpcServerTransportSession>` 多会话管理，但存在缺陷（见下文） |
| 3 | IPC 传输层缺少客户端实现 | 没有 `IpcClientTransport`，也没有任何测试保证服务端实现的正确性 |

## MCP 官方规范对自定义传输层的要求

> 摘自 [MCP Specification 2025-11-25 - Custom Transports](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports#custom-transports)：
>
> *"Clients and servers MAY implement additional custom transport mechanisms to suit their specific needs. The protocol is transport-agnostic and can be implemented over any communication channel that supports bidirectional message exchange. Implementers who choose to support custom transports MUST ensure they preserve the JSON-RPC message format and lifecycle requirements defined by MCP."*

**核心要求**：

1. **保持 JSON-RPC 消息格式**：所有消息必须是合规的 JSON-RPC 2.0
2. **保持生命周期要求**：`initialize` → `initialized` → 正常操作 → 关闭
3. **支持双向消息交换**：服务端可主动向客户端发送请求（如 sampling/createMessage、roots/list）

---

## 第一部分：InProcess 传输层 → 一服务器对多客户端

### 1.1 问题分析

当前架构：

```
McpServerBuilder
  .WithInProcess(out var pair)  ← 每次调用创建一个 InProcessTransportPair
                                   且注册一个 InProcessServerTransport
```

每个 `InProcessTransportPair` 是严格一对一的（两条 `Channel<string>`），每个 `InProcessServerTransport` 持有一个 pair 和一个 session。如果用户想连接多个客户端，必须多次调用 `WithInProcess`，导致注册多个独立的 `IServerTransport` 实例。

这对用户来说有两个问题：
- 服务器构建时必须提前知道客户端数量（pair 在 Build 前就要创建）
- 无法在运行时动态添加客户端连接

### 1.2 设计方案：引入 `InProcessServer`

#### 核心思路

引入一个 `InProcessServer` 类作为"连接入口点"，类似 HTTP 服务器的监听端点。客户端通过此对象动态建立连接，每次连接创建独立的 `InProcessTransportPair` 和对应的 session。

#### 新增类型

```
InProcessServer           — 服务端连接入口，持有引用供客户端连接
InProcessServerTransport  — 改造：持有 InProcessServer 而非单个 pair
InProcessTransportPair    — 保持不变（仍为一对一连接管道）
```

#### API 设计

**服务端**（构建时无需预知客户端数量）：

```csharp
var server = new McpServerBuilder("TestMcpServer", "1.0.0")
    .WithInProcess(out var inProcessServer)  // 返回 InProcessServer 实例
    .WithTools(t => t.WithTool(() => new SimpleTool()))
    .Build();

await server.StartAsync();
```

**客户端**（运行时动态连接）：

```csharp
// 客户端 1
var client1 = new McpClientBuilder()
    .WithInProcess(inProcessServer)  // 通过 InProcessServer 建立连接
    .Build();

// 客户端 2（可随时创建）
var client2 = new McpClientBuilder()
    .WithInProcess(inProcessServer)
    .Build();
```

// 注：我想到一个有趣的实现方式。如果我们不使用 `out var xxx` 而是直接把 `server` 的实例传到 `WithInProcess(server)` 里去呢？然后，在 `Build` 的时候，检查 server 的传输层里面有没有 InProcess 的实例，如果有就找到（可能涉及到拿到传输层列表然后判断是否是 InProcess 的服务端传输层），如果没有，就要抛出异常指导开发者要实现 InProcess 的传输层，必须在 server 创建时调用 `WithInProcess()` 来创建 InProcessServer 实例。这样，到时候对使用容器的程序也会更友好一些，因为放一个全功能的 `Server` 就够了，不需要放内部的某个类型。

#### 内部实现

```
InProcessServer:
├── ConnectAsync() → 创建新的 InProcessTransportPair
│                  → 创建新的 InProcessServerTransportSession
│                  → 通过 _manager.Add(session) 注册到 ServerTransportManager
│                  → 启动该 pair 的 RunLoopAsync
│                  → 返回 pair 供客户端使用
│
├── _manager: IServerTransportManager（在 StartAsync 时注入）
└── _sessions: ConcurrentDictionary<string, InProcessServerTransportSession>
```

#### 向后兼容

保留现有的 `WithInProcess(out InProcessTransportPair transportPair)` API 用于简单的一对一场景：

```csharp
// 旧 API（仍然可用，一对一）
.WithInProcess(out var transportPair)

// 新 API（一对多）
.WithInProcess(out var inProcessServer)
```

因为 `out` 参数类型不同（`InProcessTransportPair` vs `InProcessServer`），两个重载不会冲突。

// 注：不需要保留兼容的 API，因为我们的库还没有发布。而且保留一对一 API 并不会让代码变得简单。

### 1.3 实施步骤

| 步骤 | 任务 | 文件 |
|------|------|------|
| 1.1 | 创建 `InProcessServer` 类 | `Transports/InProcess/InProcessServer.cs` |
| 1.2 | 改造 `InProcessServerTransport`，支持以 `InProcessServer` 模式运行 | `Transports/InProcess/InProcessServerTransport.cs` |
| 1.3 | 在 `McpServerBuilder` 添加 `WithInProcess(out InProcessServer)` 重载 | `Servers/McpServerBuilder.cs` |
| 1.4 | 在 `McpClientBuilder` 添加 `WithInProcess(InProcessServer)` 重载 | `Clients/McpClientBuilder.cs` |
| 1.5 | 编写单元测试：多客户端并发连接与独立通信 | `tests/` |
| 1.6 | 更新文档 | `docs/zh-hans/InProcessTransport.md`, `docs/en/InProcessTransport.md` |

---

## 第二部分：IPC 服务端传输层修复

### 2.1 问题分析

IPC 服务端的 `IpcServerTransport` 已有多会话数据结构 (`ConcurrentDictionary<string, IpcServerTransportSession>`)，但存在以下缺陷：

#### 缺陷 1：会话未注册到 `ServerTransportManager`

```csharp
// IpcServerTransport.OnPeerConnected
private void OnPeerConnected(object? sender, PeerConnectedArgs e)
{
    var session = new IpcServerTransportSession(_manager, e.Peer.PeerName);
    session.SetPeer(e.Peer);
    _sessions[e.Peer.PeerName] = session;  // ← 仅存在本地字典
    // ❌ 缺少 _manager.Add(session);
}
```

没有调用 `_manager.Add(session)` 将会话注册到 `ServerTransportManager`，导致服务端无法通过 `TryGetSession` 找到会话。

#### 缺陷 2：请求处理缺少会话上下文

```csharp
case JsonRpcRequest request:
{
    var response2 = await _manager.HandleRequestAsync(request, null, _runningCancellationToken);
    //                                                         ^^^^ null!
}
```

`HandleRequestAsync` 的 `additionalServices` 参数为 `null`，没有通过 `services.AddTransportSession(session, Log)` 注入当前会话。对比 InProcess 实现：

```csharp
// InProcessServerTransport 正确地注入了会话
await _manager.HandleRequestAsync(
    new JsonRpcRequest { ... },
    services => services.AddTransportSession(_session, Log),
    cancellationToken);
```

**影响**：`initialize` 握手时无法正确设置 `ConnectedClientCapabilities`，sampling 等需要会话的功能无法工作。

#### 缺陷 3：重连处理不安全

```csharp
private void OnPeerReconnected(object? sender, IPeerReconnectedArgs e)
{
    var peer = (PeerProxy)sender!;
    var session = new IpcServerTransportSession(_manager, peer.PeerName);
    session.SetPeer(peer);
    _sessions[peer.PeerName] = session;  // 旧 session 未 Dispose
}
```

重连时直接覆盖旧 session，没有 Dispose 旧 session（取消挂起请求）。

### 2.2 修复方案

| 步骤 | 修复内容 | 文件 |
|------|---------|------|
| 2.1 | `OnPeerConnected` 中添加 `_manager.Add(session)` | `IpcServerTransport.cs` |
| 2.2 | 请求/通知处理中注入会话上下文 `services.AddTransportSession(session, Log)` | `IpcServerTransport.cs` |
| 2.3 | `OnPeerReconnected` 中先 Dispose 旧 session | `IpcServerTransport.cs` |
| 2.4 | `OnPeerConnectionBroken` 中 Dispose 被移除的 session | `IpcServerTransport.cs` |

---

## 第三部分：IPC 客户端传输层实现

### 3.1 设计方案

参照 `InProcessClientTransport` 的模式，实现 `IpcClientTransport`。

#### dotnetCampus.Ipc 库的使用模式

```csharp
// 创建客户端 IpcProvider（可不指定管道名，或指定自己的管道名以供服务端回连）
var clientIpcProvider = new IpcProvider();

// 连接到服务端
var peer = await clientIpcProvider.GetAndConnectPeerProxyAsync("ServerPipeName");

// 发送消息（使用 MCP 专用 Header 0x70634D2E70636E44）
await peer.NotifyAsync(new IpcMessage("", body, McpIpcHeader));

// 接收消息
peer.MessageReceived += (sender, args) => { /* 处理 */ };
```

#### 类设计

```csharp
public sealed class IpcClientTransport : IClientTransport
{
    private readonly IClientTransportManager _manager;
    private readonly string _serverPipeName;
    private readonly IpcConfiguration? _ipcConfiguration;
    private IpcProvider? _clientProvider;
    private PeerProxy? _serverPeer;

    // ConnectAsync: 创建 IpcProvider → 连接到服务端管道 → 注册 MessageReceived
    // DisconnectAsync: 取消消息循环 → Dispose IpcProvider
    // SendMessageAsync: 序列化为 JSON → 通过 peer.NotifyAsync 发送（带 McpIpcHeader）
    // 消息接收循环: MessageReceived → 解析 → 分发响应/处理服务端请求
}
```

#### API 设计

**McpClientBuilder 扩展**（放在 `DotNetCampus.ModelContextProtocol.Ipc` 项目中）：

```csharp
var client = new McpClientBuilder()
    .WithDotNetCampusIpc("ServerPipeName")
    .Build();

// 或复用已有 IpcProvider
var client = new McpClientBuilder()
    .WithDotNetCampusIpc(existingIpcProvider, "ServerPipeName")
    .Build();
```

### 3.2 实施步骤

| 步骤 | 任务 | 文件 |
|------|------|------|
| 3.1 | 实现 `IpcClientTransport` | `Transports/Ipc/IpcClientTransport.cs` |
| 3.2 | 实现 `McpClientBuilderIpcExtensions` 扩展方法 | `Clients/McpClientBuilderExtensions.cs` |
| 3.3 | 编写集成测试：IPC 客户端-服务端端到端测试 | `tests/` |
| 3.4 | 编写集成测试：IPC 多客户端并发测试 | `tests/` |

---

## 第四部分：测试计划

### 4.1 InProcess 多客户端测试

- [ ] 单客户端连接（回归测试，确保旧 API 不受影响）
- [ ] 多客户端同时连接同一服务器
- [ ] 各客户端独立调用工具并获取各自的响应
- [ ] 客户端断开连接不影响其他客户端
- [ ] 服务端对特定客户端发起 sampling 请求
- [ ] 并发压力测试（多客户端同时发送请求）

### 4.2 IPC 端到端测试

- [ ] IPC 客户端连接服务端并完成 initialize 握手
- [ ] 客户端调用工具并获取响应
- [ ] 多客户端同时连接同一 IPC 服务器
- [ ] 客户端断开 / 重连
- [ ] 服务端主动向客户端发送请求（sampling）
- [ ] 与现有 IpcProvider 业务消息共存（Header 过滤）

---

## 实施顺序建议

```
阶段 1：修复 IPC 服务端缺陷（第二部分）
        ↓ 最小改动，为后续测试奠定基础
阶段 2：实现 IPC 客户端（第三部分）
        ↓ 有了客户端才能写端到端测试来验证服务端
阶段 3：InProcess 多客户端支持（第一部分）
        ↓ 较大的架构改动，可独立进行
阶段 4：完整测试（第四部分）
        ↓ 覆盖所有场景
```

## 涉及的文件汇总

### 新增文件

| 文件 | 用途 |
|------|------|
| `src/.../Transports/InProcess/InProcessServer.cs` | InProcess 多客户端连接入口 |
| `src/.../Ipc/Transports/Ipc/IpcClientTransport.cs` | IPC 客户端传输层 |
| `src/.../Ipc/Clients/McpClientBuilderExtensions.cs` | McpClientBuilder IPC 扩展 |
| `tests/.../InProcessMultiClientTests.cs` | InProcess 多客户端测试 |
| `tests/.../IpcTransportTests.cs` | IPC 端到端测试 |

### 修改文件

| 文件 | 改动 |
|------|------|
| `src/.../Transports/InProcess/InProcessServerTransport.cs` | 支持 `InProcessServer` 模式 |
| `src/.../Servers/McpServerBuilder.cs` | 新增 `WithInProcess(out InProcessServer)` |
| `src/.../Clients/McpClientBuilder.cs` | 新增 `WithInProcess(InProcessServer)` |
| `src/.../Ipc/Transports/Ipc/IpcServerTransport.cs` | 修复会话注册、上下文注入、重连处理 |
| `docs/zh-hans/InProcessTransport.md` | 更新文档 |
| `docs/en/InProcessTransport.md` | 更新文档 |
