# HTTP 服务端传输层实现指南 (Server Transport)

> 本文档指导 `DotNetCampus.ModelContextProtocol` 库中 HTTP 服务端传输层的正确实现方式。
> 适用目标：`LocalHostHttpServerTransport` (内置) 和 `TouchSocketHttpServerTransport` (扩展)。
> **协议版本**：仅支持 MCP 2025-11-25，不兼容旧版。

## 1. 架构设计

为了保证两个实现（LocalHost 和 TouchSocket）行为的规范与一致性，我们定义了统一的逻辑流程。
**注意**：由于 `System.Net` 和 `TouchSocket` 拥有完全不同的类型系统（如 Context, Request, Response 对象），因此很难在代码层面进行深度复用。这里的“解耦”更多是指**逻辑解耦**——即 Session 管理、协议检查、SSE 格式化等纯逻辑应尽量保持独立，避免依赖特定 HTTP 库。

### 核心职责

1.  **端点 (Endpoint) 路由**：拦截特定路径（如 `/mcp`）的请求。
2.  **安全防护**：检查 `Origin` Header。
3.  **会话管理 (Session Management)**：维护 `SessionId` 到 `IServerTransportSession` 的映射。
4.  **消息分发**：区别处理 POST（JSON-RPC 消息）和 GET（SSE 连接）。
5.  **版本协商**：确保客户端支持最低版本 `2025-03-26`，优先使用 `2025-11-25`。

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
2.  **协议版本检查 (Protocol Version Check)**：
    *   读取 `MCP-Protocol-Version` header。
    *   如果 Header 不存在，服务端应默认视为 `2025-03-26` 处理（或更高兼容）。
    *   如果 Header 存在但低于 `2025-03-26`，或者包含不支持的版本格式，**必须**返回 `400 Bad Request`（参考官方规范 §2.7）。
3.  **特殊处理 - 初始化**：
    *   如果请求体解析出的 JSON-RPC method 是 `initialize`：
        *   创建一个新的 Session。
        *   生成唯一的 `Mcp-Session-Id`。
        *   将此 ID 写入响应 Header `Mcp-Session-Id`。
        *   继续处理消息。
4.  **常规处理 - 非初始化**：
    *   如果 Header 缺少 `Mcp-Session-Id` 或 ID 对应的 Session 不存在：
        *   返回 `400 Bad Request` 或 `404 Not Found` (未找到会话)。
        *   不要处理消息体。
5.  **消息处理**：
    *   反序列化 Body 为 `JsonRpcMessage`。
    *   将消息通过 `OnMessageReceived` 传递给上层 MCP Server 处理。
6.  **响应写入**：
    *   **`initialize` 请求**：
        *   设置 `Content-Type: application/json`。
        *   写入响应 JSON。
        *   返回 `200 OK`。
    *   **`JsonRpcResponse`（客户端回弹采样结果）**：
        *   返回 `202 Accepted`，无 Body。
    *   **`JsonRpcNotification`（客户端发送的通知）**：
        *   返回 `202 Accepted`，无 Body。
    *   **所有其他 `JsonRpcRequest`（工具调用等）**：
        *   设置 `Content-Type: text/event-stream`，建立本次请求的专属 SSE 流。
        *   先发送一个空注释事件（prime event）保活。
        *   将此 SSE 流绑定到当前 Session（供采样等服务端主动请求使用）。
        *   调用 `HandleRequestAsync` 处理请求（期间采样请求将写入此 SSE 流）。
        *   将最终响应写入 SSE 流，关闭流。

### C. 处理 GET 请求 (SSE Subscription)

`HandleGetRequestAsync(context)`

1.  **协商检查**：检查 `Accept` header 是否包含 `text/event-stream`。若不包含，**必须**返回 `405 Method Not Allowed`（参考官方规范 §2.2.3）。
2.  **Session 关联**：
    *   **必须**要求 Header `Mcp-Session-Id`。
    *   如果 Header 不存在（未提供 ID），**必须**返回 `400 Bad Request`（参考官方规范 §2.5.2：服务端应返回 400 而非 404）。
    *   如果 ID 存在，获取对应的 Session 对象。如果 Session 不存在，返回 `404 Not Found`（参考官方规范 §2.5.3）。
3.  **建立连接**：
    *   设置响应 Header `Content-Type: text/event-stream`。
    *   设置 `Cache-Control: no-cache`。
    *   返回 `200 OK`（此时不要关闭 Response 流）。
4.  **发送 Prime Event**：
    *   按照官方规范 §2.1.6 的 SHOULD 建议，应立即发送一个包含事件 ID 和空 data 字段的 SSE 事件，以便客户端设置 `Last-Event-ID` 用于断线重连。
    *   当前实现发送一个空注释 `:\n\n` 作为简化版保活信号（不含事件 ID，不支持断线续传）。如需支持 Resumability，应改为发送带 ID 的真实事件。
5.  **保持循环**：
    *   进入 `await Task.Delay(-1)` 等待，保持 SSE 连接存活（此通路用于未来扩展服务端主动推送，当前暂不发送任何业务消息）。
    *   在循环中捕获异常，如果连接断开则正常退出。

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
*   **并发模型**：这是关键。`HttpListener.GetContextAsync` 是一个接一个的。需要在一个循环中获取 Context，然后 `Task.Run` 处理它，不要阻塞主循环。
*   **SSE 写入**：使用 `context.Response.OutputStream.WriteAsync`，每次写入后记得 `FlushAsync`。尽量使用 `StreamWriter` 并设置 `AutoFlush = true` 来简化操作。

### TouchSocketHttpServerTransport

*   **优势**：相比 `HttpListener` 必须需要管理员权限才能监听非 localhost 地址，TouchSocket 可以轻松监听 `0.0.0.0`，极其适合局域网部署和远程调试场景。
*   **插件机制**：继承 `HttpPluginBase`。
*   **请求拦截**：在 `OnHttpRequest` 中判断 `e.Context.Request.Url` 是否匹配。
*   **SSE 支持**：需要确保 TouchSocket 支持类似 `Chunked` 传输或长连接保持。通常需要将处理模式设置为不要立即关闭连接，并持续向 `HttpResponse` 写入数据。


## 4. 关键数据结构：Session Store

需要一个线程安全的 `ConcurrentDictionary<string, HttpServerTransportSession>`。

**`HttpServerTransportSession` 类职责**：
*   存储 Session ID。
*   和待决服务端请求的 TCS 字典（继承自 `ServerTransportSession` 基类）。
*   管理当前 POST 请求的专属 SSE 输出流（`_currentRequestSseStream`），这是采样等服务端主动请求的通道。
*   提供 `WriteSseMessageAsync(Stream, JsonRpcMessage)` 方法：将消息序列化为 SSE 格式 (`event: message\ndata: {...}\n\n`) 并写入流。

## 5. 错误处理

*   **JSON 序列化错误**：返回 400。
*   **内部异常**：返回 500，并在 Body 中包含（或不包含）JSON-RPC Error。

## 6. 实现状态 (Checklist)

*   [x] POST/GET/DELETE 共用同一个 Endpoint URL `/mcp`。
*   [x] Session ID 的生成（initialize 时）和校验（后续请求）。
*   [x] 非 initialize 的 POST 请求返回 `text/event-stream`，套接 sampling 等服务端主动请求通道。
*   [x] 初始化请求返回 `application/json`。
*   [x] SSE prime event 保活连接。
*   [ ] 旧版协议兼容 (`/mcp/sse`, `/mcp/messages`)（目前未实现）。

