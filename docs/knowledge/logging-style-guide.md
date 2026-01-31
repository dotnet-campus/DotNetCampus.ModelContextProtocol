# MCP 库日志编写规范

本文档规定了 DotNetCampus.ModelContextProtocol 库内部代码使用 `IMcpLogger` 记录日志的风格规范。

## 概述

本规范适用于 MCP 库内部代码（传输层、协议桥接器、请求处理器等）使用 `IMcpLogger` 记录的日志。这些日志用于调试、监控和故障排查，帮助开发者理解库的运行状态。

> **注意**：本规范不适用于 MCP 协议层面的 `logging/setLevel` 和 `notifications/message` 消息，那些是服务端向客户端发送的协议消息，请参阅 [logging-usage-guide.md](./logging-usage-guide.md)。

## 日志格式

### 整体结构

```
[组件标签] 描述消息. 上下文键值对
```

示例：

```csharp
Log.Info($"[McpServer][StreamableHttp] Listening on {prefixes}, endpoint: {endpoint}");
Log.Debug($"[McpServer][Mcp] Received request. Method={method}, Id={id}");
```

### 组件标签

标签采用层级嵌套的中括号格式：`[顶级组件][子组件]`

**设计原则**：标签只使用静态字符串，不包含动态内容（如会话 ID、方法名）。这便于业务端通过标签进行日志过滤和聚合，动态上下文信息应放在消息体中。

#### 标签层级

| 层级     | 说明         | 可选值                                                         |
| -------- | ------------ | -------------------------------------------------------------- |
| 顶级组件 | MCP 角色     | `McpServer`、`McpClient`                                       |
| 子组件   | 传输层或模块 | `StreamableHttp`、`TouchSocket`、`Stdio`、`Ipc`、`Http`、`Mcp` |

#### 子组件说明

| 子组件           | 说明                                   |
| ---------------- | -------------------------------------- |
| `StreamableHttp` | 使用 `HttpListener` 的本机 HTTP 服务端 |
| `TouchSocket`    | 使用 TouchSocket 库的 HTTP 服务端      |
| `Stdio`          | 标准输入/输出传输层                    |
| `Ipc`            | 进程间通信传输层                       |
| `Http`           | HTTP 客户端传输层                      |
| `Mcp`            | MCP 协议桥接器和请求处理逻辑           |

#### 标签示例

| 场景               | 标签                          |
| ------------------ | ----------------------------- |
| HTTP 服务器 (本机) | `[McpServer][StreamableHttp]` |
| HTTP 服务器 (TS)   | `[McpServer][TouchSocket]`    |
| Stdio 服务器       | `[McpServer][Stdio]`          |
| IPC 服务器         | `[McpServer][Ipc]`            |
| HTTP 客户端        | `[McpClient][Http]`           |
| Stdio 客户端       | `[McpClient][Stdio]`          |
| 协议层（服务端）   | `[McpServer][Mcp]`            |
| 协议层（客户端）   | `[McpClient][Mcp]`            |

### 上下文信息格式

使用键值对格式记录动态上下文，便于日志解析工具提取结构化数据：

```csharp
// ✅ 正确：标签静态，动态信息使用 Key=Value 格式
Log.Debug($"[McpServer][StreamableHttp] SSE connection started. SessionId={SessionId}");
Log.Debug($"[McpServer][Mcp] Received request. Method={method}, Id={id}");
Log.Info($"[McpServer][Mcp] Calling tool. ToolName={toolName}");

// ❌ 错误：标签包含动态内容
Log.Debug($"[McpServer][StreamableHttp][{SessionId}] SSE connection started.");

// ❌ 错误：使用冒号或自然语言格式
Log.Info($"[McpServer][Mcp] Session: {sessionId}");
Log.Info($"[McpServer][Mcp] Calling tool {toolName}");
```

## 日志级别

### 级别定义

| 级别      | 方法              | 使用场景                                         |
| --------- | ----------------- | ---------------------------------------------------- |
| Debug     | `Log.Debug()`     | 详细调试信息，用于开发和故障排查                     |
| Info      | `Log.Info()`      | 重要运行状态，记录关键操作节点                     |
| Notice    | `Log.Notice()`    | 正常但重要的事件                                     |
| Warning   | `Log.Warn()`      | 可恢复的问题或需要注意的情况                     |
| Error     | `Log.Error()`     | 单次操作失败，但服务可继续运行                   |
| Critical  | `Log.Critical()`  | 组件故障，服务核心功能不可用                     |
| Alert     | `Log.Alert()`     | 需要人工干预的问题（安全、数据损坏等）           |
| Emergency | `Log.Emergency()` | 整个系统崩溃，无法提供任何服务                   |

### Error 与 Critical 的区别

这两个级别容易混淆，关键区别在于 **服务是否可以继续运行**：

| 级别     | 影响范围         | 服务状态       | 典型场景                               |
| -------- | ---------------- | -------------- | -------------------------------------- |
| Error    | 单次操作失败     | 可继续运行     | 请求处理失败、客户端连接失败、SSE 写入失败 |
| Critical | 组件级别故障     | 核心功能不可用 | 服务端监听器启动失败、端口无法绑定         |

**关于客户端连接失败**：MCP 主机通常管理多个客户端，单个客户端连接失败（如 STDIO 进程启动失败、HTTP 连接超时）应使用 `Error` 而非 `Critical`，因为其他客户端仍可正常工作。

示例对比：

```csharp
// Error: 单个客户端连接失败，MCP 主机还能用其他客户端
Log.Error($"[McpClient][Stdio] Failed to start STDIO process.", ex);

// Error: 单次请求失败，不影响其他请求
Log.Error($"[McpServer][StreamableHttp] Unhandled exception in request handler.", ex);

// Critical: 服务端监听器启动失败，整个 MCP 服务器无法工作
Log.Critical($"[McpServer][StreamableHttp] Failed to start listener.", ex);
```

### Alert 与 Emergency 的使用

这两个级别在 MCP 库中 **极少使用**，因为它们通常用于应用程序级别而非库级别的问题：

| 级别      | 含义                 | MCP 库中的潜在场景                       |
| --------- | -------------------- | ------------------------------------------ |
| Alert     | 需要 **人工干预**   | 检测到协议版本严重不兼容、检测到潜在安全问题 |
| Emergency | 系统 **完全不可用** | 库代码中几乎不会使用，由应用程序决定         |

> **注意**：作为库代码，我们无法判断“系统不可用”，因为 MCP 可能只是应用的一个功能模块。库代码最高一般使用到 `Critical` 级别，由上层应用决定是否达到 `Emergency`。

### 级别选择指南

```
需要记录日志？
├─ 详细技术细节（消息 ID、连接状态变化、请求/响应处理过程）？ → Debug
├─ 重要运行状态（启动、停止、会话生命周期、关键业务操作）？ → Info
├─ 可恢复的问题（重试、验证失败、未知通知）？ → Warning
├─ 错误但服务仍可运行？ → Error
└─ 严重问题需要立即关注？ → Critical/Alert/Emergency
```

### 各级别使用场景

#### Debug

记录详细的技术信息，在生产环境通常会被过滤：

```csharp
// 请求/响应流程
Log.Debug($"[McpServer][Mcp] Received request. Method={method}, Id={id}");
Log.Debug($"[McpServer][Mcp] Received notification. Method={method}");
Log.Debug($"[McpServer][Mcp] Sending success response. Method={method}, Id={id}");

// 连接生命周期细节
Log.Debug($"[McpServer][StreamableHttp] SSE connection started. SessionId={SessionId}");
Log.Debug($"[McpServer][StreamableHttp] SSE connection ended. SessionId={SessionId}");

// 列表查询
Log.Debug($"[McpServer][Mcp] Listing tools. Count={count}");
Log.Debug($"[McpServer][Mcp] Listing resources. Count={count}");

// 资源读取
Log.Debug($"[McpServer][Mcp] Reading resource. Uri={uri}");
Log.Debug($"[McpServer][Mcp] Resource read completed. Uri={uri}");

// 工具调用成功
Log.Debug($"[McpServer][Mcp] Tool call completed successfully. ToolName={toolName}");

// 非关键异常
Log.Debug($"[McpServer][StreamableHttp] Exception during dispose. Error={ex.Message}");
```

#### Info

记录重要的运行状态和关键操作节点：

```csharp
// 服务器启动/停止
Log.Info($"[McpServer][StreamableHttp] Listening on {prefixes}, endpoint: {endpoint}");
Log.Info($"[McpServer][StreamableHttp] Transport stopped.");
Log.Info($"[McpServer][Stdio] Transport started.");
Log.Info($"[McpServer][Ipc] Disposing transport.");

// 会话生命周期
Log.Info($"[McpServer][TouchSocket] Session created. SessionId={sessionId}");
Log.Info($"[McpServer][TouchSocket] Session terminated. SessionId={sessionId}");

// 协议握手
Log.Info($"[McpServer][Mcp] Client initializing. ClientName={name}, ClientVersion={version}, ProtocolVersion={protocolVersion}");
Log.Info($"[McpServer][Mcp] Server initialized. ServerName={name}, ServerVersion={version}, ToolCount={count}, ResourceCount={count}");
Log.Info($"[McpServer][Mcp] Client initialized notification received. Session is now fully established.");

// 关键业务操作
Log.Info($"[McpServer][Mcp] Calling tool. ToolName={toolName}");

// 客户端连接状态
_logger.Info($"[McpClient][Http] Transport connected.");
_logger.Info($"[McpClient][Http] Session negotiated. SessionId={sessionId}");
_logger.Info($"[McpClient][Http] SSE receive loop started.");
_logger.Info($"[McpClient][Http] SSE receive loop stopped.");
```

#### Warning

记录可恢复的问题或需要注意的情况：

```csharp
// 重试场景
_logger.Warn($"[McpClient][Http] Failed to terminate session, will retry. SessionId={sessionId}, Error={ex.Message}");
_logger.Warn($"[McpClient][Http] SSE connection error, reconnecting. Error={ex.Message}");

// 请求验证失败
Log.Warn($"[McpServer][TouchSocket] POST request rejected: Missing Mcp-Session-Id header.");
Log.Warn($"[McpServer][TouchSocket] POST request rejected: Session not found. SessionId={sessionId}");
Log.Warn($"[McpServer][TouchSocket] GET request rejected: Client must accept text/event-stream.");

// 工具调用验证失败
Log.Warn($"[McpServer][Mcp] Tool call rejected: Tool name is required.");
Log.Warn($"[McpServer][Mcp] Tool call rejected: Unknown tool. ToolName={toolName}");
Log.Warn($"[McpServer][Mcp] Tool call completed with error. ToolName={toolName}");
Log.Warn($"[McpServer][Mcp] Tool call failed: Missing required argument. ToolName={toolName}, Error={ex.Message}");

// 资源读取验证失败
Log.Warn($"[McpServer][Mcp] Resource read rejected: URI is required.");
Log.Warn($"[McpServer][Mcp] Resource read rejected: Resource not found. Uri={uri}");

// 未知/不支持的操作
Log.Warn($"[McpServer][Mcp] Received unsupported notification. Method={method}");
```

#### Error

记录单次操作失败，但服务可继续运行：

```csharp
// 客户端连接失败（MCP 主机还可以用其他客户端）
Log.Error($"[McpClient][Stdio] Failed to start STDIO process.", ex);
Log.Error($"[McpClient][Http] Failed to connect to server.", ex);

// 单次请求处理失败（不影响其他请求）
Log.Error($"[McpServer][StreamableHttp] Unhandled exception in request handler.", ex);
Log.Error($"[McpServer][StreamableHttp] Failed to write SSE message. SessionId={SessionId}", ex);

// 协议层单次处理失败
Log.Error($"[McpServer][Mcp] HandleInitializeAsync failed. Error={ex.Message}");
Log.Error($"[McpServer][Mcp] HandleCallToolAsync failed. Error={ex.Message}");

// 工具调用异常
Log.Error($"[McpServer][Mcp] Tool call failed: Service not found. ToolName={toolName}, Error={ex.Message}");
Log.Error($"[McpServer][Mcp] Tool call failed: Unexpected error. ToolName={toolName}, Error={ex.Message}");

// 会话冲突（罕见但不影响其他会话）
Log.Error($"[McpServer][TouchSocket] Session ID collision. SessionId={sessionId}");
```

#### Critical

记录组件级别故障，服务核心功能不可用：

```csharp
// 服务端传输层启动失败 - 整个 MCP 服务器无法工作
Log.Critical($"[McpServer][StreamableHttp] Failed to start listener.", ex);
Log.Critical($"[McpServer][TouchSocket] Failed to bind to port.", ex);

// 注意：客户端连接失败不是 Critical，因为 MCP 主机可以管理多个客户端，
// 单个客户端失败不影响其他客户端的正常工作。
```

## 消息文本规范

### 语言要求

所有日志消息必须使用英文：

- 与代码和协议规范保持一致
- 便于在日志系统中搜索和过滤
- 便于国际团队协作和社区贡献
- 部分日志分析工具对非 ASCII 字符处理不佳

### 标点符号

| 规则           | 正确                              | 错误                                |
| -------------- | --------------------------------- | ----------------------------------- |
| 使用英文标点   | `Failed to start listener.`       | `Failed to start listener。`        |
| 上下文用键值对 | `SessionId={id}, Method={method}` | `SessionId: {id}, Method: {method}` |
| 消息末尾用句号 | `Transport started.`              | `Transport started`                 |
| 动词用现在分词 | `Handling request...`             | `Handle request`                    |

### 动词时态

| 场景         | 时态              | 示例                                     |
| ------------ | ----------------- | ---------------------------------------- |
| 操作正在进行 | 现在进行时 (-ing) | `Handling JSON-RPC request...`           |
| 操作即将开始 | 现在进行时 (-ing) | `Starting transport.`                    |
| 操作已完成   | 过去式/名词化     | `Session created.`, `Transport stopped.` |
| 状态描述     | 现在时            | `Listening on ...`                       |
| 错误发生     | 过去式            | `Failed to start.`                       |

### 消息结构模式

**状态描述**：

```csharp
Log.Info($"[McpServer][StreamableHttp] Listening on {prefixes}, endpoint: {endpoint}");
```

**操作进行**：

```csharp
Log.Debug($"[McpServer][Mcp] Handling request. Method={method}, Id={id}");
Log.Info($"[McpServer][Mcp] Calling tool. ToolName={toolName}");
```

**操作完成**：

```csharp
Log.Info($"[McpServer][TouchSocket] Session terminated. SessionId={sessionId}");
Log.Debug($"[McpServer][Mcp] Tool call completed successfully. ToolName={toolName}");
```

**错误报告**：

```csharp
Log.Error($"[McpServer][StreamableHttp] Failed to start listener.", ex);
```

**请求拒绝**：

```csharp
Log.Warn($"[McpServer][TouchSocket] POST request rejected: Missing session ID header.");
Log.Warn($"[McpServer][Mcp] Tool call rejected: Unknown tool. ToolName={toolName}");
```

### 上下文信息

日志应包含足够的上下文信息以便调试：

**应包含**：

- 会话标识（SessionId）
- 请求标识（Id/MessageId）
- 方法名（Method）
- 工具名（ToolName）
- 资源 URI（Uri）
- 关键状态参数（StatusCode、Count 等）

**不应包含**：

- 敏感信息（密码、密钥、Token）
- 个人身份信息（PII）
- 完整的请求/响应体
- 堆栈跟踪（使用异常参数传递）

### 异常处理

传递异常对象而非序列化异常：

```csharp
// ✅ 正确：传递异常对象
Log.Error($"[McpServer][StreamableHttp] Failed to start listener.", ex);

// ✅ 正确：只需异常消息时使用 ex.Message
Log.Warn($"[McpClient][Http] SSE connection error, reconnecting. Error={ex.Message}");

// ❌ 错误：在消息中序列化完整异常
Log.Error($"[McpServer][StreamableHttp] Failed to start listener: {ex}");
```

## 代码实现

### 日志属性命名

```csharp
private IMcpLogger Log => _manager.Context.Logger;        // 传输层
protected IMcpLogger Logger => _server.Context.Logger;    // 处理器类
private readonly IMcpLogger _logger;                      // 字段存储
```

### 使用扩展方法

```csharp
// ✅ 推荐：使用扩展方法
Log.Debug($"[McpServer][StreamableHttp] SSE connection ended. SessionId={sessionId}");
Log.Info($"[McpServer][StreamableHttp] Listener stopped.");
Log.Error($"[McpServer][StreamableHttp] Failed to start listener.", ex);

// ❌ 不推荐：直接调用 Log 方法
logger.Log(LoggingLevel.Debug, "...", null, (s, ex) => s);
```

### 字符串插值

```csharp
// ✅ 推荐：使用字符串插值
Log.Info($"[McpServer][StreamableHttp] Listening on {prefixes}, endpoint: {endpoint}");

// ❌ 不推荐：使用字符串拼接
Log.Info("[McpServer][StreamableHttp] Listening on " + prefixes + ", endpoint: " + endpoint);
```

## 检查清单

编写新日志时，确保：

- [ ] 标签只含静态内容，使用 `[McpServer/McpClient][子组件]` 格式
- [ ] 动态信息在消息体中，使用 `Key=Value` 格式
- [ ] 级别选择恰当，根据决策指南选择
- [ ] 使用英文消息和英文标点符号
- [ ] 消息末尾使用句号
- [ ] 动词时态正确（进行中 -ing，完成 -ed）
- [ ] 不含敏感信息
- [ ] 异常通过参数传递，而非在消息中序列化
- [ ] 使用扩展方法和字符串插值

## 参考资源

- [IMcpLogger 接口](../../src/DotNetCampus.ModelContextProtocol/Hosting/Logging/IMcpLogger.cs)
- [McpLoggerExtensions 扩展方法](../../src/DotNetCampus.ModelContextProtocol/Hosting/Logging/McpLoggerExtensions.cs)
- [LoggingLevel 枚举](../../src/DotNetCampus.ModelContextProtocol/Protocol/Messages/LoggingLevel.cs)
- [MCP 协议日志指南](./logging-usage-guide.md)
- [RFC 5424 - Syslog Protocol](https://datatracker.ietf.org/doc/html/rfc5424#section-6.2.1)
