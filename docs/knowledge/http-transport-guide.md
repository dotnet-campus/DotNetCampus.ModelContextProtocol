# MCP HTTP 传输层实现指南

本文档总结了 `HttpServerTransport` 实现 MCP 协议的关键知识点。

## 📌 协议版本支持

| 版本       | 发布日期   | 名称            | 端点                        | 会话管理                 | 状态     |
| ---------- | ---------- | --------------- | --------------------------- | ------------------------ | -------- |
| **最新**   | 2025-11-25 | Streamable HTTP | `/mcp`                      | `Mcp-Session-Id` header  | ✅ 已支持 |
|            | 2025-06-18 | Streamable HTTP | `/mcp`                      | `Mcp-Session-Id` header  | ✅ 已支持 |
| **变更**   | 2025-03-26 | Streamable HTTP | `/mcp`                      | `Mcp-Session-Id` header  | ✅ 已支持 |
| **旧协议** | 2024-11-05 | HTTP+SSE        | `/mcp/sse`, `/mcp/messages` | query string `sessionId` | ✅ 兼容   |

> **说明**: 2025-11-25、2025-06-18 和 2025-03-26 在传输层上完全兼容，我们的实现同时支持这些版本。

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

```csharp
#region 新协议实现 (Streamable HTTP - 2025-03-26+)
// HandleSseConnectionAsync()
// HandleJsonRpcRequestAsync()
// HandleDeleteSessionAsync()
#endregion

#region 旧协议兼容 (HTTP+SSE - 2024-11-05)
// HandleLegacySseConnectionAsync()      // 带 Legacy 前缀
// HandleLegacyMessageRequestAsync()
#endregion
```

## ✅ 测试清单

- [ ] 新协议：POST `/mcp` 返回 `Mcp-Session-Id`
- [ ] 新协议：GET `/mcp` 建立 Streamable HTTP 连接
- [ ] 新协议：DELETE `/mcp` 成功终止会话
- [ ] 旧协议：GET `/mcp/sse` 发送 endpoint 事件
- [ ] 旧协议：POST `/mcp/messages?sessionId=xxx` 正常工作
- [ ] 路径大小写不敏感
- [ ] 会话不存在时 DELETE 返回 200 OK（幂等性）

## 📚 相关文档

- [详细开发指南](../.github/copilot-instructions.md)
- [MCP 官方规范 2025-11-25](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports) - **最新版本**
- [MCP 官方规范 2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
- [MCP 官方规范 2025-03-26](https://modelcontextprotocol.io/specification/2025-03-26/basic/transports)
- [MCP 官方规范 2024-11-05](https://modelcontextprotocol.io/specification/2024-11-05/basic/transports)
