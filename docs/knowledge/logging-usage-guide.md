# MCP 日志系统使用指南

## 概述

MCP 的日志系统允许**服务端向客户端发送日志消息**，让客户端能够监控服务端的运行状态。这不是用来控制服务端自己的日志记录，而是用于向客户端（AI 应用）报告运行信息。

## 工作原理

### 1. 客户端设置日志级别

客户端发送 `logging/setLevel` 请求来设置想要接收的最低日志级别：

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "logging/setLevel",
  "params": {
    "level": "info"
  }
}
```

服务端会保存这个级别到 `McpServerContext.LoggingLevel`。

### 2. 服务端发送日志消息

服务端通过 SSE 连接向客户端发送 `notifications/message` 通知：

```json
{
  "jsonrpc": "2.0",
  "method": "notifications/message",
  "params": {
    "level": "error",
    "logger": "database",
    "data": {
      "error": "Connection failed",
      "details": {
        "host": "localhost",
        "port": 5432
      }
    }
  }
}
```

## 日志级别（按严重性从低到高）

| 级别        | 用途             | 示例场景           |
| ----------- | ---------------- | ------------------ |
| `debug`     | 详细调试信息     | 函数进入/退出点    |
| `info`      | 一般信息性消息   | 操作进度更新       |
| `notice`    | 正常但重要的事件 | 配置变更           |
| `warning`   | 警告条件         | 使用了已弃用的功能 |
| `error`     | 错误条件         | 操作失败           |
| `critical`  | 严重条件         | 系统组件故障       |
| `alert`     | 必须立即采取行动 | 检测到数据损坏     |
| `emergency` | 系统不可用       | 完整系统故障       |

## 何时发送日志

服务端应该在以下场景发送日志：

### ✅ 应该发送的日志

- **操作进度** (info) - 长时间运行的任务的进度更新
- **配置变更** (notice) - 重要配置已更改
- **警告信息** (warning) - 非致命但需要注意的问题
- **错误信息** (error) - 操作失败但服务可继续运行
- **严重问题** (critical/alert/emergency) - 系统级故障

### ❌ 不应发送的日志

- 每个函数调用的 debug 信息（太频繁）
- 敏感信息（密码、密钥、个人身份信息）
- 内部系统细节（可能被用于攻击）

## 实现方式

### 方式一：在 McpServerContext 中添加发送日志方法（推荐）

你说"关于日志，我有我的想法"，所以我不会在这里实现具体的日志发送逻辑。但是根据 MCP 协议，日志发送需要：

1. **访问 SSE 会话**：需要能够向所有或特定的客户端 SSE 连接发送通知
2. **级别过滤**：只发送达到或高于 `McpServerContext.LoggingLevel` 的日志
3. **序列化为 JSON**：构造 `LoggingMessageNotification` 并通过 SSE 发送

### 方式二：在业务代码中手动发送

如果你想在业务代码（如工具实现）中发送日志，可以：

1. 获取当前的 `McpServerContext`
2. 检查 `context.LoggingLevel` 是否允许发送该级别的日志
3. 构造 `LoggingMessageNotification` 对象
4. 通过某种方式（事件、回调等）传递给传输层发送

## 示例场景

### 场景 1：工具执行进度

```csharp
// 在工具执行时发送进度日志
public async ValueTask<CallToolResult> ExecuteAsync(...)
{
    // 发送 info 级别日志：开始执行
    await SendLog(LoggingLevel.Info, "file-processor", new { 
        message = "开始处理文件", 
        fileName = fileName 
    });

    try
    {
        // 处理文件...
        
        // 发送 info 级别日志：完成
        await SendLog(LoggingLevel.Info, "file-processor", new { 
            message = "文件处理完成", 
            fileName = fileName,
            itemsProcessed = count
        });
    }
    catch (Exception ex)
    {
        // 发送 error 级别日志
        await SendLog(LoggingLevel.Error, "file-processor", new { 
            error = ex.Message, 
            fileName = fileName 
        });
        
        throw;
    }
}
```

### 场景 2：数据库连接警告

```csharp
// 数据库连接池即将耗尽时发送警告
if (connectionPool.AvailableConnections < 2)
{
    await SendLog(LoggingLevel.Warning, "database", new {
        message = "数据库连接池即将耗尽",
        available = connectionPool.AvailableConnections,
        total = connectionPool.TotalConnections
    });
}
```

## 安全注意事项

根据 MCP 规范，日志消息**必须不包含**：

- ❌ 凭据或密钥
- ❌ 个人身份信息（PII）
- ❌ 可能帮助攻击者的内部系统细节

## 性能建议

- 实现速率限制，避免日志洪水
- 对于高频操作，使用更高的日志级别（warning 或更高）
- 考虑使用结构化数据（JSON 对象）而不是纯字符串
- 在 `data` 字段中包含相关上下文信息

## 技术实现要点

### 需要解决的问题

1. **如何从业务代码访问 SSE 会话？**
   - 可以在 `McpServerContext` 中添加事件或回调
   - 可以创建一个日志桥接器，由 `HttpServerTransport` 订阅

2. **如何区分不同客户端？**
   - 每个 SSE 会话有独立的 `sessionId`
   - 每个会话可能有不同的 `LoggingLevel` 设置
   - 需要支持"广播"（向所有客户端发送）或"单播"（向特定客户端发送）

3. **如何确保线程安全？**
   - SSE 写入需要同步（`StreamWriter` 不是线程安全的）
   - 建议使用 `Channel<T>` 或类似的异步队列

## 总结

- `LoggingLevel` 属性用于**过滤**哪些日志应该发送给客户端
- 日志通过 `notifications/message` 通知从服务端**主动推送**给客户端
- 服务端应该在有意义的时机发送日志，帮助客户端了解运行状态
- 具体的发送实现需要与传输层（`HttpServerTransport`）配合
