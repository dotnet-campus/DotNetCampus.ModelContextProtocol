# 12 - Streamable HTTP compliance and resumability

## 目标

让 LocalHost 和 TouchSocket 两套 Streamable HTTP 实现严格遵循 `2025-11-25` transport 规范，并支持可靠的 SSE 恢复。

## 前置任务

- 07 - Server notifications and list_changed support
- 10 - Cancellation and progress

## 任务列表

- [ ] 两套服务器统一校验 POST Content-Type 和 Accept。
- [ ] 统一 MCP-Protocol-Version、Mcp-Session-Id 和 HTTP status 行为。
- [ ] SSE 消息生成唯一 event id。
- [ ] 客户端解析 SSE `id:` 与 `retry:`。
- [ ] 服务端保存可恢复事件并处理 `Last-Event-ID`。
- [ ] GET SSE stream 能承载服务端主动请求和通知。
- [ ] 处理多连接、断线重连、session 过期和消息重放。
- [ ] 复核 Origin/DNS rebinding 和公网监听安全策略。
- [ ] 为 LocalHost/TouchSocket 建立共享 transport conformance tests。

## 完成标准

- 两套 HTTP 实现在同一测试矩阵下行为一致。
- 非法 Content-Type/Accept/header 得到规范 HTTP 响应。
- 断线后客户端可用 Last-Event-ID 恢复且不丢失已承诺消息。
- 服务端主动消息不再依赖当前 POST SSE stream。

## 官方依据

- [Transports](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports)

## 当前代码证据

- TouchSocket Accept 校验使用 OR：`src/DotNetCampus.ModelContextProtocol.TouchSocket.Http/Transports/TouchSocket/TouchSocketHttpServerTransport.cs:918-930`。
- LocalHost POST 未进行同等 Accept/Content-Type 校验：`src/DotNetCampus.ModelContextProtocol/Transports/Http/LocalHostHttpServerTransport.cs:392-400`。
- SSE 当前没有 event id：`src/DotNetCampus.ModelContextProtocol/Transports/Http/HttpServerTransportSession.cs:13-102`。
- Server-initiated request 当前依赖 POST 绑定流：`src/DotNetCampus.ModelContextProtocol/Transports/Http/HttpServerTransportSession.cs:66-74`。

