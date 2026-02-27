# MCP HTTP 传输层规范分析与总结 (2025-11-25)

> 本文档基于 [MCP 官方规范 2025-11-25](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports) 进行分析，旨在厘清客户端和服务端的职责边界，以及核心概念的实现要求。

## 1. 核心概念辨析：端 (Endpoint) vs 会话 (Session)

在实现 HTTP 传输层时，必须清晰区分这两个概念：

### 端 (Endpoint) - 物理入口
*   **定义**：一个固定的 URL 地址（例如 `https://example.com/mcp`）。
*   **性质**：无状态，它是所有 HTTP 请求的目标地址。
*   **动词支持**：必须同时支持 `POST`（发送消息）、`GET`（建立监听流）和 `DELETE`（销毁会话）。

### 会话 (Session) - 逻辑连接
*   **定义**：客户端与服务端之间的一组逻辑相关的交互，由 `Mcp-Session-Id` 唯一标识。
*   **生命周期**：
    1.  **创建**：客户端发送 `InitializeRequest`，服务端在响应头中返回 `Mcp-Session-Id`。
    2.  **维持**：客户端后续的所有请求（POST/GET/DELETE）**必须**带上此 Header。
    3.  **销毁**：客户端发送 `DELETE` 请求，或服务端超时清理。
*   **作用**：将多个独立的 HTTP 请求关联到同一个上下文（Context）中。SSE 流是附属于某个 Session 的。

---

## 2. 职责清单 (Checklist)

### 🖥️ 服务端 (Server) 实现要求

#### 必做 (MUST / REQUIRED)
1.  **单一端点**：提供一个同时支持 POST, GET, DELETE 的 URL。
2.  **安全性检查**：必须验证 `Origin` header，防止 DNS rebinding 攻击（本地运行时仅绑定 127.0.0.1）。
3.  **POST 请求处理**：
    *   解析 JSON-RPC 消息体。
    *   **Request 消息**：
        *   支持返回 `Content-Type: application/json`（单次直接响应）。
        *   支持返回 `Content-Type: text/event-stream`（升级为 SSE 流式响应）。
    *   **Notification/Response 消息**：如果接受，返回 `202 Accepted`（无 Body）。
    *   **错误处理**：对于不支持的版本或格式错误，返回 400 及其它适当的 HTTP 状态码。
4.  **GET 请求处理**：
    *   如果 `Accept` 包含 `text/event-stream`，必须建立 SSE 连接（或返回 405 不支持）。
    *   **关键限制**：在纯监听的 GET SSE 流上，**绝不能**发送 JSON-RPC Response（除非是 Resumability 恢复场景），只能发送 Server 端的 Request 或 Notification。
5.  **会话管理**：
    *   在 `InitializeResult` 响应中分配并返回 `Mcp-Session-Id` header。
    *   验证后续请求中的 `Mcp-Session-Id`。如果 ID 无效或过期，返回 404。
    *   处理 `DELETE` 请求以终止会话。
6.  **协议版本**：检查 `MCP-Protocol-Version` header。

#### 选做 (SHOULD / MAY)
1.  **流式恢复 (Resumbaility)**：支持 `Last-Event-ID` header，允许客户端在断开后恢复 SSE 流并重发丢失的消息。
2.  **SSE 优化**：在 SSE 连接建立时立即发送一个空事件以 "prime" 客户端。
3.  **主动断开**：为了避免长连接及资源占用，服务端 *可以* 随时关闭 SSE 连接（客户端应通过 Polling 机制重连）。

---

### 📱 客户端 (Client) 实现要求

#### 必做 (MUST / REQUIRED)
1.  **发送消息 (POST)**：
    *   所有 JSON-RPC 消息必须通过 POST 发送到端点。
    *   Headers 必须包含：
        *   `Accept: application/json, text/event-stream`
        *   `MCP-Protocol-Version: 2025-11-25` (或协商版本)
        *   `Mcp-Session-Id: <ID>` (初始化后必带)
2.  **接收响应**：
    *   必须能处理 `application/json`（直接 JSON 响应）。
    *   必须能处理 `text/event-stream`（SSE 流式响应）。
3.  **会话管理**：
    *   保存初始化响应中的 `Mcp-Session-Id`。
    *   在不再需要会话时，发送 `DELETE` 请求。

#### 选做 (SHOULD / MAY)
1.  **建立独立监听流 (GET)**：可以发送 GET 请求建立一个专用的 SSE 接收通道（此时服务端可以随时推送消息，而不必等待 POST 请求）。
2.  **自动重连 (Polling)**：如果 SSE 连接断开（且非致命错误），应根据 `retry` 字段或自动策略进行重连。

---

## 3. 交互模式详解

### 模式 A：经典的请求-响应 (Request-Response)
这是最简单的 HTTP 行为，类似传统 API。
1.  Client POST `CallToolRequest`
2.  Server 处理完成
3.  Server 返回 200 OK, `Content-Type: application/json`
4.  Body: `CallToolResult`

### 模式 B：流式响应 (Delayed/Streaming Response)
适用于耗时操作或需要多条消息反馈的场景。
1.  Client POST `CallToolRequest`
2.  Server 返回 200 OK, `Content-Type: text/event-stream`
3.  Server 发送 SSE event (id: 1, data: "") [Priming]
4.  ... 时间流逝 ...
5.  Server 发送 SSE event (id: 2, data: `CallToolResult` JSON)
6.  Server 关闭 SSE 连接

### 模式 C：服务端主动推送 (Server Notifications)
服务端需要主动给客户端发消息（例如 `ProgressNotification` 或 `CallToolRequest` 让客户端执行）。
1.  **前提**：Client 已经建立了一个 SSE 连接（通过之前的 POST 升级，或独立的 GET）。
2.  Server 在该 SSE 连接上推送 event (data: `JsonRpcNotification` JSON)。

## 4. 关键实现建议

如果您要实现 HTTP 传输层，建议遵循以下优先级：

1.  **优先实现 POST + application/json 响应**：这是基础，能跑通最简单的请求。
2.  **必须实现 Session ID 逻辑**：这是 2025-11-25 协议强制的，没有它无法通过官方合规性测试。
3.  **必须实现 DELETE 方法**：用于优雅关闭。
4.  **建议实现 GET SSE 监听**：虽然可以通过 POST 的 SSE 响应来推送，但独立的 GET 监听流在复杂场景（如 Server 频繁推送 Log 或 Progress）下更稳定。

## 5. 常见误区

*   **误区 1**：认为 SSE 只能用于服务端响应。
    *   **纠正**：SSE 也是服务端向客户端发送 *请求* (Server Request) 的通道。
*   **误区 2**：认为每个 Client Request 必须对应一个 HTTP Response。
    *   **纠正**：Client 发送 Notification 时，Server 仅回 202 Accepted，没有 JSON Body。
*   **误区 3**：混淆协议版本。
    *   **纠正**：如果不带 `MCP-Protocol-Version`，服务端可能会回落到 `2025-03-26` 或更早的逻辑，导致行为不一致。

