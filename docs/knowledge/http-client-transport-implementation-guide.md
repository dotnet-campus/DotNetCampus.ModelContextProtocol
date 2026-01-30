# MCP HTTP 客户端传输层实现指南

本文档基于 `DotNetCampus.ModelContextProtocol` 对于 MCP 2025-11-25 规范（Streamable HTTP）的最新成功实践，总结了 HTTP 客户端传输层的实现要点。

## 1. 核心架构：双循环模型

为了同时支持 Request/Response 通信和服务器主动推送（Notifications），传输层需要维护两个并发的逻辑流。

### 1.1 主动请求循环 (POST Transport/Loop)
负责所有由客户端发起的请求（Requests）和通知（Notifications）。

- **HTTP 方法**: `POST`
- **生命周期**: 短生命周期，每次 `SendMessageAsync` 发起一个新的请求。
- **职责**:
  1. 发送 JSON-RPC 消息。
  2. 接收直接的 JSON 响应（Happy Path）。
  3. **关键**: 处理 "瞬态 SSE 流" (Transient SSE)。如果服务器在 POST 响应中返回 `text/event-stream`，客户端必须在当前请求中就地消费这个流，直到流结束。

### 1.2 被动接收循环 (GET Loop)
负责接收服务器主动发送的消息（如 `notifications/progress` 或反向请求），以及在断连时的自动重连。

- **HTTP 方法**: `GET`
- **生命周期**: 长生命周期，在握手成功后启动，直到传输层 Dispose。
- **职责**:
  1. 维持一个对 `/mcp` 端点的长连接。
  2. 设置 header `Mcp-Session-Id` 以标识身份。
  3. 持续解析 SSE 事件 (`message`, `endpoint` 等) 并分发给 Manager。
  4. 处理网络异常和自动重连策略。

## 2. 关键实现细节

### 2.1 握手与会话管理 (Connect & Initialize)

MCP 的 HTTP 传输是无状态的，直到第一次交互。

1. **Lazy Connect**: `ConnectAsync` 可以是空操作。
2. **First Request (Initialize)**:
   - 客户端发送 `initialize` 请求。
   - **必需 Header**: `Mcp-Protocol-Version`。
   - **必需 Accept**: `application/json` 和 `text/event-stream`。
3. **Session Capture**:
   - 检查响应头 `Mcp-Session-Id`。
   - 一旦获取到 Session ID，**立即**启动后台 GET 接收循环。
   - 后续所有请求（POST 和 GET）都必须携带此 ID。

### 2.2 瞬态 SSE (Transient SSE) 处理

很多服务器实现（如 `@modelcontextprotocol/server-everything`）对于 POST 请求，倾向于返回 `text/event-stream` 而不是 `application/json`，即使是对于简单的 RPC 响应。

**处理逻辑**:
```csharp
var mediaType = response.Content.Headers.ContentType?.MediaType;
if (mediaType == "text/event-stream")
{
    // 进入瞬态 SSE 处理模式
    await ProcessSseStreamAsync(responseStream);
    // 注意：这里的流包含的是针对当前请求的响应
}
else
{
    // 标准 JSON 处理
    var json = await ReadResponseAsync();
}
```

### 2.3 SSE 解析器实现

无论是 GET Loop 还是 Transient SSE，都需要一个健壮的 SSE 解析器：

1. **基本格式**: 解析 `event:`, `data:`, `id:` 字段。
2. **多行 Data**: 标准允许 `data` 字段分多行发送，解析器需要缓存 buffer 并拼接。
3. **事件分发**:
   - `message` 事件（或空 event）：通常包含 JSON-RPC 载荷，需反序列化并交给 Manager。
   - `endpoint` 事件：Legacy 协议使用，Streamable HTTP 模式下忽略即可。

### 2.4 优雅断开 (Disconnect)

Streamable HTTP 协议要求客户端在退出时显式通知服务器。

- **动作**: 发送 `DELETE` 请求到服务端点。
- **Headers**: 必须包含 `Mcp-Session-Id`。
- **策略**: Best Effort（尽力而为），吞掉发送过程中的异常，确保本地资源清理不受网络影响。

## 3. 代码映射 (TransportLayer)

- **`SendRequestCoreAsync`**: 实现了 POST Loop 和 Transient SSE 判定逻辑。
- **`ReceiveLoopAsync`**: 实现了后台 GET Loop 和重连退避逻辑。
- **`ProcessSseStreamAsync`**: 通用的 SSE 流解析器。

本指南对应的完整实现可见于 `DotNetCampus.ModelContextProtocol.Transports.Http.HttpClientTransport`。
