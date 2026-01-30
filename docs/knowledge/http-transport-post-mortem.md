# Streamable HTTP 传输层实现模型对比分析

## 摘要

本文分析了 DotNetCampus.ModelContextProtocol 项目中 HTTP 客户端传输层的两种实现策略。通过对比“连接复用模型”（旧版）与“正交双通道模型”（新版），本文剖析了导致通信死锁、HTTP 406 错误及会话状态异常的根本原因，并结合 MCP 2025-11-25 规范论证了新版设计的正确性。

## 1. 瞬态 SSE 与持久流的生命周期管理

### 1.1 问题背景：Server-Everything 的行为模式

在 `@modelcontextprotocol/server-everything` 参考实现中，对于 POST 请求，服务端可能返回 `Content-Type: text/event-stream`。此类流具有**瞬态性（Transient）**：
*   **服务端行为**：服务端通过此流发送请求处理进度（Progress）和最终结果，发送完毕后**服务端主动关闭 HTTP 响应流**（发送 EOF 或结束 Chunked 传输）。
*   **客户端预期**：客户端应消费流中事件直至 EOF，视为当前 RPC 请求结束。

> **规范引文 (MCP 2025-11-25 §2.1.5-§2.1.6)**
>
> "If the input is a JSON-RPC request, the server MUST either return `Content-Type: text/event-stream`, to initiate an SSE stream, or `Content-Type: application/json`, to return one JSON object. The client MUST support both these cases."
>
> "After the JSON-RPC response has been sent, the server SHOULD terminate the SSE stream."

### 1.2 旧版实现：持久化误判

旧版实现错误地将 POST 响应的 `text/event-stream` 判定为连接升级（Connection Upgrade），试图将其接管为长效接收通道。
*   **缺陷**：客户端在通过 Reader 读取完 RPC 结果后，继续挂起等待后续事件。由于服务端已关闭流（EOF），`StreamReader` 会读取结束。但在旧版逻辑中，这种结束被错误处理（或在此之前因等待不存在的数据而阻塞），导致 RPC 请求无法通过 `await` 返回，引发调用端死锁。即便流正常结束，旧版逻辑也未能正确重启监听通道。

### 1.3 新版实现：就地消费策略

新版实现明确区分了“请求级流”与“会话级流”。对于 POST 响应流：
*   **处理逻辑**：在当前 `SendRequestAsync` 上下文中就地遍历流。
*   **生命周期终止**：当 `StreamReader.ReadLineAsync` 返回 `null`（标识服务端关闭流）时，循环自然退出，RPC 调用正常返回。

```csharp
// 如果 Response 是 SSE 流，视为本次请求的流式响应，也就是 Transient SSE
if (string.Equals(mediaType, "text/event-stream", ...))
{
    // 读取直到服务端关闭流 (EOF)
    // 逻辑：服务端发送完 Response 后会主动断开此流，导致 ReadLineAsync 返回 null
    await ProcessSseStreamAsync(stream, cancellationToken);
}
```

## 2. 信令通道架构

### 2.1 协议规范要求

根据 MCP 规范（Streamable HTTP），Client-to-Server 的通信通过独立的 POST 请求承载，而 Server-to-Client 的通信（如 Notifications, Server Requests）需通过持久通道承载。

> **规范引文 (MCP 2025-11-25 §2.1.1, §2.2.1)**
>
> "The client MUST use HTTP POST to send JSON-RPC messages to the MCP endpoint."
>
> "The client MAY issue an HTTP GET to the MCP endpoint. This can be used to open an SSE stream, allowing the server to communicate to the client, without the client first sending data via HTTP POST."

### 2.2 旧版实现：条件式通道

旧版采用条件分支逻辑：若 POST 连接“升级”成功，则复用该连接接收通知；仅在未升级时尝试建立 GET 连接。
*   **缺陷**：在 Transient SSE 场景下，POST 连接并未升级为持久通道（它在请求结束后关闭）。由于旧版判定逻辑缺陷，导致在 Transient SSE 模式下既未复用成功（流已关），也未启动 GET 连接，致使服务端发送的消息（如 `notifications/initialized` 之后的交互）客户端完全处于由 Server 到 Client 的失联状态。

### 2.3 新版实现：正交双通道 (Orthogonal Dual-Channel)

新版解耦了消息发送与事件接收，确立了独立并行的双循环架构：
1.  **POST Loop**：仅负责发送请求并接收该请求的直接响应（JSON 或 Transient SSE）。
2.  **GET Loop**：在握手成功（获取 SessionId）后，**无条件**启动独立的后台 GET 请求指向 SSE 端点。

此设计符合 MCP 规范中关于 Transport 的职责分离，确保了 Server-to-Client 通道的持续可用性。无条件启动 GET Loop 是为了保证无论此前 POST 请求因何种格式返回，客户端始终拥有一个接收服务端推送的回路。

## 3. 内容协商 (Content Negotiation)

### 3.1 HTTP 406 问题

旧部分代码在 `SendNotificationAsync` 中仅设置了 `Accept: application/json`。部分严格遵循规范的服务端（如 server-everything）在处理特定请求时，强制要求客户端同时支持 SSE 降级（即 Accept 必须包含 `text/event-stream`），否则拒绝服务并返回 `406 Not Acceptable`。这通常是因为服务端希望保留返回流式错误的权利。

> **规范引文 (MCP 2025-11-25 §2.1.2)**
>
> "The client MUST include an `Accept` header, listing both `application/json` and `text/event-stream` as supported content types."

### 3.2 修正方案

新版实现遵循规范建议，在所有 POST 请求中统一设置 Accept Header：
```csharp
request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
```
这允许服务端根据负载情况自主选择返回普通 JSON 还是流式 SSE。

### 3.3 会话与版本标头

> **规范引文 (MCP 2025-11-25 §2.5.2, §2.7)**
>
> "If an `MCP-Session-Id` is returned by the server... clients ... MUST include it in the `MCP-Session-Id` header on all of their subsequent HTTP requests."
>
> "The client MUST include the `MCP-Protocol-Version: <protocol-version>` HTTP header on all subsequent requests to the MCP server".

新版实现确保了所有的 POST 和 GET 请求都携带了这两个关键标头。

## 4. 结论

旧版实现的失败归因于对 MCP Streamable HTTP 传输模型的误解——试图在单一 HTTP 事务中混合处理 RPC 响应与长效事件监听。新版实现通过**生命周期分离**（Transient vs Persistent）与**通道职责分离**（POST vs GET），解决了死锁与消息丢失问题，完全符合 MCP 2025-11-25 规范的要求。
