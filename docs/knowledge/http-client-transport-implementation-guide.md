# HTTP 客户端传输层实现指南 (Client Transport)

> 本文档指导 `DotNetCampus.ModelContextProtocol` 库中 HTTP 客户端传输层的正确实现方式。
> 适用目标：`HttpClientTransport` (内置)。
> **协议版本**：仅支持 MCP 2025-11-25，不兼容旧版。

## 1. 架构设计

`HttpClientTransport` 需要作为 MCP Client 的网络适配器，负责维护与服务端的连接状态。与简单的 HTTP 请求不同，MCP 客户端需要维护一个长效的会话（Session）。

### 核心组件

1.  **Transport Client**：持有 `HttpClient` 实例，负责发送 POST 请求。
2.  **SSE Reader (Event Loop)**：一个后台任务，负责长时间运行 GET 请求，监听服务端推送。
3.  **Session Manager**：存储当前的 `SessionId` 和协议版本。

## 2. 生命周期流程

### A. 连接 (ConnectAsync)

客户端启动时的序列：

1.  **准备 HttpClient**：配置 Base Address 或 Endpoint URL。
2.  **发送 Initialize (Handshake via POST)**：
    *   构造一个 `InitializeRequest` 的 JSON-RPC 消息。
    *   发送 POST 请求到 Endpoint。
    *   **Header 要求**：
        *   `Accept: application/json, text/event-stream`
        *   `MCP-Protocol-Version: 2025-11-25`
    *   **处理响应 (Upgrade Check)**：
        *   **Check Content-Type**：
            *   如果是 `application/json`：
                *   读取 `Mcp-Session-Id` header (REQUIRED)。
                *   解析 Body (`InitializeResult`)。
                *   **下一步**：启动一个独立的 GET 请求任务去建立 SSE 监听（参见 Step 3）。
            *   如果是 `text/event-stream`（Server 升级策略）：
                *   读取 `Mcp-Session-Id` header (REQUIRED)。
                *   **立即**将此 Response Stream 视为 SSE Event Loop。
                *   在此流中等待第一个 SSE Event，其中应包含 `InitializeResult`。
                *   一旦收到 `InitializeResult`，完成 `ConnectAsync` 的握手部分，并将剩余的 Stream 读取转交给后台任务（Message Loop）。
                *   **注意**：在此模式下，不需要再发起额外的 GET 请求。

3.  **建立 SSE 监听 (Start Listening)**：
    *   *仅当 Initialize 未升级为 SSE 时执行此步骤。*
    *   启动一个后台 Task。
    *   Task 发起 GET 请求到 Endpoint。
    *   Header 包含 `Mcp-Session-Id` 和 `Accept: text/event-stream`。
    *   一旦连接建立，开始循环读取流数据。

4.  **发送 Initialized 通知 (Handshake Completion)**：
    *   客户端在收到 `InitializeResult` 后，**必须**发送一个 `notifications/initialized` 通知。
    *   该调用使用 `SendMessageAsync` 发送一个 `JsonRpcNotification`。
    *   Header 必须包含 `Mcp-Session-Id`。
    *   此步骤标志着握手完成，客户端准备好处理请求。

### B. 发送消息 (SendMessageAsync)

当上层 Client 想要调用工具或发送通知时：

1.  **前置检查**：检查 Session ID 是否存在（即是否已连接）。
2.  **构造请求**：
    *   HTTP Method: `POST`
    *   Header:
        *   `Mcp-Session-Id`: <当前 Session ID>
        *   `Content-Type`: `application/json`
3.  **发送与接收**：
    *   发送 HTTP 请求。
    *   **情况 1 (200 OK + JSON Body)**：
        *   服务端直接返回了结果（例如 `CallToolResult`）。
        *   反序列化 Body 为 `JsonRpcMessage`。
        *   **注意**：这里有一个路由问题。MCP Client 架构通常通过统一的 `ProcessMessage` 方法处理所有入站消息。如果 POST 直接返回了结果，我们需要手动将这个结果“注入”到入站消息队列中，以便让等待响应的 `Request` 对象能匹配到它。
    *   **情况 2 (202 Accepted)**：
        *   服务端已接收，但没有立即返回结果（或者是 Notification）。
        *   不做任何处理，结果（如果有）稍后会通过 SSE 收到。
    *   **情况 3 (200 OK + SSE)**：
        *   极少见。如果发生，应像 Initialize 那样临时读取流。但通常只有 Initialize 会这样做。

### C. 接收消息 (Message Loop)

在后台的 SSE Listener Task 中：

1.  读取 HTTP Response Stream。
2.  解析 SSE 格式 (`event: ...`, `data: ...`)。
3.  **Event 分发**：
    *   忽略 `event: endpoint` (旧协议残留) 或心跳包。
    *   关注 `message` 事件（或默认事件）。
    *   读取 `data` 字段内容，反序列化为 `JsonRpcMessage`。
    *   调用 `OnMessageReceived` 回调，将消息交给 MCP 客户端核心层处理。
4.  **断线重连**：
    *   如果流意外中断（抛出异常或读取结束），应尝试自动重连。
    *   重连时，Header 带上 `Last-Event-ID`（如果服务端支持断点续传）。
    *   如果收到 401/403/404，这通常意味着 Session 失效，应停止重连并触发 `Close` 事件。

### D. 关闭 (CloseAsync)

1.  **发送 Termination**：
    *   发送 `DELETE` 请求到 Endpoint。
    *   Header 带上 `SessionId`。
2.  **清理资源**：
    *   取消 SSE Listener Task 的 CancellationToken。
    *   释放 HttpClient (如果在内部创建)。
    *   清空 Session ID。

## 3. 关键实现细节

### 处理 POST 响应与 SSE 推送的竞态

MCP 协议允许通过 POST 的 Response 返回 JSON-RPC Result，也可以通过 SSE 返回。
*   **实现策略**：`HttpClientTransport` 在收到 POST 的 `200 OK` (JSON) 时，应该直接将这个 JSON 对象作为收到的消息触发 `OnMessageReceived`。
*   这就像是从 SSE 收到的一样，上层的 `McpClient` 会根据 ID 匹配 Request。

### Session ID 的保存

Session ID 是有状态的。`HttpClientTransport` 必须是一个有状态的对象，不能每次 `SendMessageAsync` 都重新创建 HttpClient 或忽略之前的 Context。

### 代码结构建议

```csharp
public class HttpClientTransport : IClientTransport
{
    private readonly HttpClient _httpClient;
    private string? _sessionId;
    private Task? _sseLoopTask;
    private CancellationTokenSource? _connectionCts;

    // 连接
    public async Task ConnectAsync(...) {
        // 1. Send Initialize (POST) -> Get Session Id
        // 2. Start SSE Loop (GET)
    }

    // 循环监听
    private async Task SseLoopAsync() {
        // GET /mcp with SessionId
        // Read stream line by line
        // OnMessageReceived(msg)
    }

    // 发送
    public async Task SendMessageAsync(JsonRpcMessage message) {
        // POST /mcp with SessionId
        // IF 200 OK & Content-Type == application/json:
        //    var respMsg = Deserialize(body);
        //    OnMessageReceived(respMsg); // Inject response
    }
}
```

## 4. 错误处理

*   **HTTP 404 on POST**：Session 过期。应抛出异常或触发断开连接事件。
*   **HTTP 400**：请求格式错误。
*   **连接重试**：鉴于本库零依赖的原则，建议手写简单的指数退避（Exponential Backoff）重试逻辑来处理 SSE 连接的暂时性网络抖动。更复杂的策略（如引入 Polly）应在扩展包中实现，避免核心库添加额外依赖。

## 5. 待办事项 (Checklist)

*   [ ] 移除所有关于 `/mcp/sse` 的旧逻辑。
*   [ ] 实现 `POST /mcp` 的初始化握手，提取 `Mcp-Session-Id`。
*   [ ] 确保在 `InitializeResult` 后能正常发送 `notifications/initialized` 通知。
*   [ ] 实现后台 Task 进行 `GET /mcp` 的 SSE 监听。
*   [ ] 实现 SSE 协议解析器（解析 `data:` 行）。
*   [ ] 确保 POST 响应可以直接将消息回注到处理管道。
*   [ ] 实现 `DELETE /mcp` 关闭逻辑。
