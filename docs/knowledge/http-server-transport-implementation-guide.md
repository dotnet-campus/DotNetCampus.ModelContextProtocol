# HTTP 服务端传输层实现指南 (Server Transport)

> 本文档指导 `DotNetCampus.ModelContextProtocol` 库中 HTTP 服务端传输层的正确实现方式。
> 适用目标：`LocalHostHttpServerTransport` (内置) 和 `TouchSocketHttpServerTransport` (扩展)。
> **协议版本**：仅支持 MCP 2025-11-25，不兼容旧版。

## 1. 架构设计

为了保证两个实现（LocalHost 和 TouchSocket）的行为一致性，核心协议逻辑应与具体 HTTP 库解耦。这两个 Transport 的主要职责是将底层的 HTTP 请求映射为 MCP 的通用操作。

### 核心职责

1.  **端点 (Endpoint) 路由**：拦截特定路径（如 `/mcp`）的请求。
2.  **安全防护**：检查 `Origin` Header。
3.  **会话管理 (Session Management)**：维护 `SessionId` 到 `IServerTransportSession` 的映射。
4.  **消息分发**：区别处理 POST（JSON-RPC 消息）和 GET（SSE 连接）。

## 2. 通用实现规范

无论底层使用 `HttpListener` 还是 `TouchSocket`，所有 HTTP 服务端传输层都**必须**遵循以下逻辑流程。

### A. 全局请求拦截 (On Request)

当收到一个 HTTP 请求时：

1.  **路径检查**：检查 URL 是否匹配配置的 Endpoint 路径（不区分大小写）。如果不匹配，忽略（交给其他处理者或返回 404）。
2.  **跨域/安全检查 (Security)**：
    *   检查 Header `Origin`。
    *   如果存在且不合法（根据配置的允许列表），**必须**返回 `403 Forbidden`。
    *   *注：本地开发通常允许空 Origin 或 localhost。*
3.  **HTTP 方法分发**：
    *   `POST`: 处理 JSON-RPC 消息。
    *   `GET`: 处理 SSE 订阅。
    *   `DELETE`: 处理会话销毁。
    *   其他: 返回 `405 Method Not Allowed`，允许 Header 包含 `POST, GET, DELETE`。

### B. 处理 POST 请求 (JSON-RPC Messages)

`HandlePostRequestAsync(context)`

1.  **读取 Session ID**：从 Header `Mcp-Session-Id` 读取。
2.  **特殊处理 - 初始化**：
    *   如果请求体解析出的 JSON-RPC method 是 `initialize`：
        *   创建一个新的 Session。
        *   生成唯一的 `Mcp-Session-Id`。
        *   将此 ID 写入响应 Header `Mcp-Session-Id`。
        *   继续处理消息。
3.  **常规处理 - 非初始化**：
    *   如果 Header 缺少 `Mcp-Session-Id` 或 ID 对应的 Session 不存在：
        *   返回 `400 Bad Request` 或 `404 Not Found` (未找到会话)。
        *   不要处理消息体。
4.  **协议版本检查**：
    *   检查 `MCP-Protocol-Version` header。虽然可以宽容处理，但最好记录或验证。
5.  **消息处理**：
    *   反序列化 Body 为 `JsonRpcMessage`。
    *   将消息通过 `OnMessageReceived` 传递给上层 MCP Server 处理。
6.  **响应写入**：
    *   **情况 1：上层有直接同步返回 (Response)**：
        *   设置 `Content-Type: application/json`。
        *   写入响应 JSON。
        *   返回 `200 OK`。
    *   **情况 2：上层无直接返回 (Notification) 或 异步处理**：
        *   返回 `202 Accepted`。
        *   无 Body。
    *   *高级情况：SSE 升级*（如果 POST 请求 accept SSE 且 Server 决定用 SSE 回复）：
        *   设置 `Content-Type: text/event-stream`。
        *   保持连接并在稍后推送 SSE Event。
        *   *建议：简单起见，POST 尽量使用 application/json 回复，推送信道留给 GET SSE。*

### C. 处理 GET 请求 (SSE Subscription)

`HandleGetRequestAsync(context)`

1.  **协商检查**：检查 `Accept` header 是否包含 `text/event-stream`。若不包含，返回 `405` 或 `400`。
2.  **Session 关联**：
    *   **必须**要求 Header `Mcp-Session-Id`。
    *   如果 ID 不存在，返回 `404 Not Found`。
    *   如果 ID 存在，获取对应的 Session 对象。
3.  **建立连接**：
    *   设置响应 Header `Content-Type: text/event-stream`。
    *   设置 `Cache-Control: no-cache`。
    *   返回 `200 OK`（此时不要关闭 Response 流）。
4.  **注册发送通道**：
    *   将当前 HTTP Response 流包装为一个 `IAsyncWriter` 或类似接口。
    *   注册到 Session 对象中，作为服务端向客户端推送消息的通道（Server-to-Client Messenger）。
    *   **注意**：一个 Session 可能同时有多个 SSE 连接（虽然不推荐，但协议允许）。或者新连接顶替旧连接。建议实现为：新连接加入，旧连接断开或共存。
5.  **发送 Prime Event**：
    *   立即发送一个空事件 `event: message\ndata: \n\n` 或仅 `:\n\n` (Comment) 以保活。
    *   根据 SSE 规范，发送 `id` 字段以支持重连。
6.  **保持循环**：
    *   进入 `await Task.Delay(-1)` 或等待 Session 关闭信号。
    *   在循环中捕获异常，如果连接断开，从 Session 中注销此通道。

### D. 处理 DELETE 请求 (Session Termination)

`HandleDeleteRequestAsync(context)`

1.  从 Header `Mcp-Session-Id` 读取 ID。
2.  查找 Session。
    *   如果找到：销毁 Session（触发 `OnClosed`，断开关联的 SSE 连接），返回 `200 OK`。
    *   如果未找到：返回 `404` 或 `200 OK`（幂等性）。

---

## 3. 具体实现指引

### LocalHostHttpServerTransport (`System.Net.HttpListener`)

*   **监听器**：使用 `HttpListener` 绑定前缀（如 `http://127.0.0.1:8080/mcp/`）。
*   **并发模型**：这是关键。`HttpListener.GetContextAsync` 是一个接一个的。需要在一个循环中获取 Context，然后 `Task.Run` 处理它，不要阻塞主循环，否则无法处理并发请求（主要是一个 GET SSE 挂着时，POST 进不来）。
*   **SSE 写入**：使用 `context.Response.OutputStream.WriteAsync`，每次写入后记得 `FlushAsync`。

### TouchSocketHttpServerTransport

*   **插件机制**：继承 `HttpPluginBase`。
*   **请求拦截**：在 `OnHttpRequest` 中判断 `e.Context.Request.Url` 是否匹配。
*   **SSE 支持**：TouchSocket 的 `HttpResponse` 支持分块传输。需要确认是否支持保持连接不关闭并持续写入。通常需要将 `Context.Response` 标记为已处理但不关闭，然后在另一个 Task 中控制写入。或者使用 WebSockets (但 MCP 标准是 SSE)。
    *   *提示*: TouchSocket 可能需要特殊配置以支持长连接流式写入而不超时。

## 4. 关键数据结构：Session Store

需要一个线程安全的 `ConcurrentDictionary<string, HttpServerSession>`。

**`HttpServerSession` 类职责**：
*   存储 Session ID。
*   管理 SSE 发送通道（也就是当前挂着的那个 GET Response 流）。
*   提供 `SendMessageAsync(JsonRpcMessage)` 方法：将消息序列化为 SSE 格式 (`event: message\ndata: {...}\n\n`) 并写入流。

## 5. 错误处理

*   **JSON 序列化错误**：返回 400。
*   **内部异常**：返回 500，并在 Body 中包含（或不包含）JSON-RPC Error。

## 6. 待办事项 (Checklist)

*   [ ] 移除旧版兼容代码 (`/mcp/sse`, `/mcp/messages` 路径处理)。
*   [ ] 确保 POST/GET/DELETE 共用同一个 Endpoint URL。
*   [ ] 实现 Session ID 的生成（初始化时）和校验（后续请求）。
*   [ ] 实现 SSE 的心跳或 Keep-Alive（如果底层不自动处理）。

