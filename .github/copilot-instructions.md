# MCP HTTP 传输层开发指南

## 协议版本分界

### 最新协议：Streamable HTTP (2025-06-18 / 2025-03-26+)
- **官方文档**: 
  - https://modelcontextprotocol.io/specification/2025-06-18/basic/transports
  - https://modelcontextprotocol.io/specification/2025-03-26/basic/transports
- **端点**: `/mcp` (单一端点，支持 GET/POST/DELETE)
- **会话管理**: 通过 `Mcp-Session-Id` HTTP header
- **说明**: 2025-06-18 和 2025-03-26 在传输层上完全兼容，主要差异在于消息层（batching）
- **特征**:
  - POST 到 `/mcp` 处理所有 JSON-RPC 消息
  - GET 到 `/mcp` 建立 SSE 连接（不发送 `endpoint` 事件）
  - DELETE 到 `/mcp` 终止会话（必须带 `Mcp-Session-Id` header）
  - Initialize 请求时，服务器在响应头返回 `Mcp-Session-Id`
  - 后续请求都需要携带 `Mcp-Session-Id` header

### 旧协议：HTTP+SSE (2024-11-05)
- **官方文档**: https://modelcontextprotocol.io/specification/2024-11-05/basic/transports#http-with-sse
- **端点**: `/mcp/sse` (SSE), `/mcp/messages` (POST)
- **会话管理**: 通过 query string 传递 `sessionId`
- **特征**:
  - GET 到 `/mcp/sse` 必须发送 `event:endpoint` 事件（告知客户端消息端点）
  - POST 到 `/mcp/messages?sessionId=xxx` 处理消息
  - 通过 SSE 返回响应（`event:message` + `data:...`）

## 关键实现要点

### 1. 路径比较必须使用正确的分隔符
```csharp
// ❌ 错误：Path.Join 在 Windows 上会生成 \mcp\sse
private string LegacySsePath => Path.Join(EndPoint, "sse");

// ✅ 正确：使用字符串插值，确保 HTTP 路径使用 /
private string LegacySsePath => $"{EndPoint}/sse";
```

### 2. 大小写不敏感的路径匹配
```csharp
// ✅ 使用 StringComparison.OrdinalIgnoreCase 避免 ToLower() 的字符串分配
if (endpoint.Equals(EndPoint, StringComparison.OrdinalIgnoreCase))
```

### 3. 流式序列化/反序列化优化
```csharp
// ✅ 直接从流反序列化，避免字符串分配
var request = await JsonSerializer.DeserializeAsync(
    inputStream, 
    McpServerRequestJsonContext.Default.JsonRpcRequest
);

// ✅ 直接序列化到流
await JsonSerializer.SerializeAsync(
    ctx.Response.OutputStream, 
    response, 
    McpServerResponseJsonContext.Default.JsonRpcResponse
);

// ⚠️ SSE 格式例外：因为需要嵌入特定文本格式，可以直接用 Serialize
var responseText = JsonSerializer.Serialize(response, ...);
await writer.WriteAsync($"data:{responseText}\n\n");
```

### 4. DELETE 请求处理（新协议）
```csharp
// 必须支持 DELETE /mcp 来终止会话
// 官方规范: §2.5 Session Management Point 5
private async Task HandleDeleteSessionAsync(HttpListenerContext ctx)
{
    var sessionId = ctx.Request.Headers["Mcp-Session-Id"];
    
    // 验证 sessionId
    if (string.IsNullOrEmpty(sessionId))
    {
        RespondWithError(ctx, HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
        return;
    }
    
    // 移除会话（即使不存在也返回 200 OK，保证幂等性）
    if (_sseSessions.TryRemove(sessionId, out var session))
    {
        session.CancellationToken.Cancel();
        session.CancellationToken.Dispose();
    }
    
    RespondWithSuccess(ctx, HttpStatusCode.OK);
}
```

### 5. CORS 头配置
```csharp
response.Headers.Add("Access-Control-Allow-Origin", "*");
response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Mcp-Session-Id");
```

### 6. SSE 响应头配置
```csharp
response.ContentType = "text/event-stream";
response.Headers.Add("Cache-Control", "no-cache,no-store");
response.Headers.Add("Content-Encoding", "identity");
response.Headers.Add("Connection", "keep-alive");
response.StatusCode = (int)HttpStatusCode.OK;
```

## 客户端协议检测逻辑（向后兼容）

根据官方规范 §2.8 Backwards Compatibility：

1. 客户端 POST `initialize` 到 `/mcp`
   - **成功** → 新协议 (Streamable HTTP)
   - **返回 4xx** → 尝试 GET `/mcp`
2. 客户端 GET 到 `/mcp`
   - **收到 `event:endpoint`** → 旧协议 (HTTP+SSE)
   - **没有 endpoint 事件** → 新协议

因此服务器实现：
- `/mcp` GET: 不发送 `endpoint` 事件（新协议）
- `/mcp/sse` GET: 必须发送 `endpoint` 事件（旧协议兼容）

## 代码组织规范

### 使用 region 区分协议
```csharp
#region 新协议实现 (Streamable HTTP - 2025-03-26+)
// 新协议的所有方法
#endregion

#region 旧协议兼容 (HTTP+SSE - 2024-11-05)
// 旧协议的所有方法，方法名带 Legacy 前缀
#endregion

#region 辅助方法
// 通用辅助方法
#endregion
```

### 命名约定
- 新协议方法: `HandleSseConnectionAsync`, `HandleJsonRpcRequestAsync`, `HandleDeleteSessionAsync`
- 旧协议方法: `HandleLegacySseConnectionAsync`, `HandleLegacyMessageRequestAsync` (带 `Legacy` 前缀)
- 旧协议端点: `LegacySsePath`, `LegacyMessagePath`

### 日志标记
- 新协议: `[McpServer][Http]`
- 旧协议: `[McpServer][Http][Legacy]`

## 常见错误排查

### 客户端提示 "404 not found from MCP server"
- **原因**: 路径匹配失败（通常是 `Path.Join` 使用了错误的分隔符）
- **解决**: 使用 `$"{EndPoint}/sse"` 而不是 `Path.Join(EndPoint, "sse")`

### "Failed to terminate session: Not Found"
- **原因**: 未实现 DELETE 请求处理
- **解决**: 添加 `HandleDeleteSessionAsync` 方法并在路由中处理 DELETE

### 客户端识别为旧协议
- **原因**: POST 到 `/mcp` 返回 404，导致客户端降级
- **解决**: 确保 POST `/mcp` 路由正确配置

## 性能优化建议

1. ✅ 使用流式序列化/反序列化
2. ✅ 使用 `StringComparison.OrdinalIgnoreCase` 避免 `ToLower()` 分配
3. ✅ 使用 `StreamWriter.AutoFlush = true` 减少 SSE 延迟
4. ✅ 异常处理要完善，避免连接泄漏
5. ✅ 使用 `CancellationToken` 优雅关闭 SSE 连接

## 测试检查清单

- [ ] POST `/mcp` 处理 `initialize` 请求并返回 `Mcp-Session-Id`
- [ ] GET `/mcp` 建立 SSE 连接（不发送 endpoint 事件）
- [ ] DELETE `/mcp` 终止会话（带 `Mcp-Session-Id`）
- [ ] GET `/mcp/sse` 建立 SSE 连接并发送 `endpoint` 事件
- [ ] POST `/mcp/messages?sessionId=xxx` 处理旧协议消息
- [ ] 路径大小写不敏感匹配
- [ ] CORS 预检请求正常响应
- [ ] 会话不存在时 DELETE 返回 200 OK（幂等性）

## 参考资源

- [MCP 2025-06-18 Transports](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
- [MCP 2025-03-26 Transports](https://modelcontextprotocol.io/specification/2025-03-26/basic/transports)
- [MCP 2024-11-05 Transports](https://modelcontextprotocol.io/specification/2024-11-05/basic/transports)
- [SSE Standard](https://html.spec.whatwg.org/multipage/server-sent-events.html)
- [JSON-RPC 2.0](https://www.jsonrpc.org/specification)
