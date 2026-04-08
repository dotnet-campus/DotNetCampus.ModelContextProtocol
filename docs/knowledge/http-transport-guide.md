# MCP HTTP 传输层实现指南

本文档总结了 `HttpServerTransport` 实现 MCP 协议的关键知识点。

## 📌 协议版本支持

| 版本       | 发布日期   | 名称            | 端点                        | 会话管理                 | 状态     |
| ---------- | ---------- | --------------- | --------------------------- | ------------------------ | -------- |
| **最新**   | 2025-11-25 | Streamable HTTP | `/mcp`                      | `Mcp-Session-Id` header  | ✅ 已支持 |
|            | 2025-06-18 | Streamable HTTP | `/mcp`                      | `Mcp-Session-Id` header  | ✅ 已支持 |
| **变更**   | 2025-03-26 | Streamable HTTP | `/mcp`                      | `Mcp-Session-Id` header  | ✅ 已支持 |
| **旧协议** | 2024-11-05 | HTTP+SSE        | `/mcp/sse`, `/mcp/messages` | query string `sessionId` | ❌ 未实现 |

> **说明**: 2025-11-25、2025-06-18 和 2025-03-26 在传输层上完全兼容，我们的实现同时支持这些版本。旧版 HTTP+SSE 协议（2024-11-05）目前未实现，如有需要请提 issue。

## 🔑 关键区别

### 新协议 (Streamable HTTP - 2025-03-26+)
- ✅ POST `/mcp` - 处理所有 JSON-RPC 消息（包括 initialize）
- ✅ GET `/mcp` - 建立 Streamable HTTP 连接
- ✅ DELETE `/mcp` - 终止会话（必须带 `Mcp-Session-Id` header）
- 📋 [官方文档 2025-11-25](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports)
- 📋 [官方文档 2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
- 📋 [官方文档 2025-03-26](https://modelcontextprotocol.io/specification/2025-03-26/basic/transports)

### 旧协议 (HTTP+SSE - 2024-11-05)
- ✅ GET `/mcp/sse` - 建立 SSE 连接（**必须**发送 `event:endpoint` 事件）
- ✅ POST `/mcp/messages?sessionId=xxx` - 处理消息
- 📋 [官方文档 2024-11-05](https://modelcontextprotocol.io/specification/2024-11-05/basic/transports#http-with-sse)

## ⚠️ 常见陷阱

### 1. Windows 路径分隔符问题
```csharp
// ❌ 错误：在 Windows 上生成 \mcp\sse
Path.Join(EndPoint, "sse")

// ✅ 正确：HTTP 路径使用 /
$"{EndPoint}/sse"
```

### 2. 必须实现 DELETE 请求
```csharp
// 新协议要求支持 DELETE /mcp 终止会话
// 官方规范: §2.5 Session Management
if (method == "DELETE" && endpoint.Equals(EndPoint, StringComparison.OrdinalIgnoreCase))
{
    await HandleDeleteSessionAsync(ctx);
}
```

### 3. endpoint 事件发送时机
- `/mcp` GET: **不发送** `event:endpoint`（新协议）
- `/mcp/sse` GET: **必须发送** `event:endpoint`（旧协议）

## 🚀 性能优化

### 流式序列化
```csharp
// ✅ 避免字符串中间分配
await JsonSerializer.DeserializeAsync(stream, context);
await JsonSerializer.SerializeAsync(stream, obj, context);
```

### 大小写不敏感比较
```csharp
// ✅ 避免 ToLower() 分配
endpoint.Equals(EndPoint, StringComparison.OrdinalIgnoreCase)
```

## 📁 代码组织

POST 处理逻辑被拆分为职责单一的方法（LocalHost 和 TouchSocket 两版结构完全对称）：

```
HandlePostRequestAsync（入口）
  ├── HandleClientResponseAsync   // 客户端响应服务端采样请求（JsonRpcResponse）
  ├── HandleNotificationAsync     // 通知消息，返回 202 Accepted
  └── HandleRpcRequestAsync       // JSON-RPC 请求
        ├── GetOrCreateSessionAsync // Session 查找/创建
        ├── HandleInitializeAsync   // initialize：返回 application/json
        └── HandleSseRequestAsync   // 其他请求：返回 text/event-stream SSE
```

> **POST 响应规则**：
> - `initialize` 请求 → `Content-Type: application/json`，直接返回
> - 所有其他 JSON-RPC 请求 → `Content-Type: text/event-stream`，
>   采样等服务端发起的消息在此流上推送，最终响应也写入此流后关闭

## ✅ 测试清单

- [x] 新协议：POST `/mcp` initialize 返回 `Mcp-Session-Id`（`application/json`）
- [x] 新协议：POST `/mcp` 工具调用返回 `text/event-stream` SSE 流
- [x] 新协议：GET `/mcp` 建立 SSE 保活连接
- [x] 新协议：DELETE `/mcp` 成功终止会话
- [x] 采样（Sampling）：服务端通过 POST 响应 SSE 流发起采样请求，客户端 POST 回采样结果
- [ ] 路径大小写不敏感
- [ ] 会话不存在时 DELETE 返回 200 OK（幂等性）

## 📚 相关文档

- [详细的 2025-11-25 协议规范分析与总结](./mcp-http-transport-spec-2025-11-25-analysis.md) - **推荐阅读**：包含端与会话的深度辨析及完整职责清单
- [HTTP 服务端传输层实现指南](./http-server-transport-implementation-guide.md) - 针对本库 `LocalHost` 和 `TouchSocket` 实现的具体指导
- [HTTP 客户端传输层实现指南](./http-client-transport-implementation-guide.md) - 针对本库 `HttpClientTransport` 重写的具体指导
- [详细开发指南](../.github/copilot-instructions.md)
- [MCP 官方规范 2025-11-25](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports) - **最新版本**
- [MCP 官方规范 2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
- [MCP 官方规范 2025-03-26](https://modelcontextprotocol.io/specification/2025-03-26/basic/transports)
- [MCP 官方规范 2024-11-05](https://modelcontextprotocol.io/specification/2024-11-05/basic/transports)
