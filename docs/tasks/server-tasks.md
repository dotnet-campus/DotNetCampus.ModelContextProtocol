# MCP 服务端实现任务列表

本文档详细列出了实现 MCP 服务端所需完成的所有任务。服务端优先实现 **MCP over Stream**，以抽象流的形式完整实现 MCP 协议。

## 架构概述

```
┌─────────────────────┐
│     应用层（用户代码）                   │
│  - IResourceProvider                     │
│  - IPromptProvider                       │
│  - IToolProvider                         │
└─────────────────────┘
                  ↓
┌─────────────────────┐
│     MCP 服务端核心                       │
│  - 生命周期管理                          │
│  - 能力协商                              │
│  - 请求路由                              │
│  - 响应管理                              │
└─────────────────────┘
                  ↓
┌─────────────────────┐
│     MCP over Stream 传输层               │
│  - JSON-RPC 消息处理                     │
│  - 流读写管理                            │
│  - 消息序列化/反序列化                   │
└─────────────────────┘
                  ↓
┌─────────────────────┐
│     流提供者（可插拔）                   │
│  - stdio (stdin/stdout)                  │
│  - HTTP + SSE (未来)                     │
│  - dotnetCampus.Ipc (未来)               │
└─────────────────────┘
```

## 第一阶段：核心协议层

### 1. JSON-RPC 2.0 消息基础设施

**优先级**: 🔴 最高

**目标**: 实现符合 JSON-RPC 2.0 规范的消息类型和序列化

**任务**:
- [ ] 定义 `JsonRpcMessage` 基类
- [ ] 实现 `JsonRpcRequest` 类型
  - `jsonrpc`: "2.0"
  - `id`: string | number（不能为 null）
  - `method`: string
  - `params`: object（可选）
- [ ] 实现 `JsonRpcResponse` 类型
  - `jsonrpc`: "2.0"
  - `id`: string | number
  - `result`: object（与 error 互斥）
  - `error`: JsonRpcError（与 result 互斥）
- [ ] 实现 `JsonRpcNotification` 类型
  - `jsonrpc`: "2.0"
  - `method`: string
  - `params`: object（可选）
  - **注意**: 不包含 `id` 字段
- [ ] 实现 `JsonRpcError` 类型
  - `code`: number
  - `message`: string
  - `data`: any（可选）
- [ ] JSON 序列化器实现（使用 System.Text.Json）
- [ ] JSON 反序列化器实现（包含类型鉴别）
- [ ] ID 生成器（确保唯一性）
- [ ] ID 验证器（确保不为 null，不重复使用）

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/basic

---

### 2. 流传输层 - MCP over Stream 基础架构

**优先级**: 🔴 最高

**目标**: 实现基于抽象流的 MCP 协议通信

**任务**:
- [ ] 定义 `IMcpStream` 接口
  ```csharp
  interface IMcpStream
  {
      ValueTask<string?> ReadLineAsync(CancellationToken ct);
      ValueTask WriteLineAsync(string line, CancellationToken ct);
      ValueTask FlushAsync(CancellationToken ct);
  }
  ```
- [ ] 实现 `StreamMessageReader`
  - 逐行读取 UTF-8 编码的 JSON-RPC 消息
  - 处理消息边界（换行符分隔）
  - 错误处理（不完整消息、编码错误等）
  - 验证消息不包含嵌入式换行符
- [ ] 实现 `StreamMessageWriter`
  - 写入换行符分隔的 JSON-RPC 消息
  - 确保每条消息后都有换行符
  - UTF-8 编码处理
  - 确保消息不包含嵌入式换行符
- [ ] 实现消息队列管理
  - 发送队列（处理并发写入）
  - 接收队列（处理并发读取）
  - 背压控制（防止内存溢出）
- [ ] 实现消息验证
  - 检查嵌入式换行符
  - 检查 UTF-8 编码合法性

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/basic/transports

---

## 第二阶段：生命周期管理

### 3. 初始化阶段

**优先级**: 🔴 最高

**目标**: 实现 MCP 连接的初始化流程

**任务**:
- [ ] 接收 `initialize` 请求
  - 解析客户端协议版本（protocolVersion）
  - 解析客户端能力（capabilities）
  - 解析客户端信息（clientInfo）
- [ ] 协议版本协商
  - 支持 `2025-06-18` 版本
  - 如果客户端版本不支持，返回服务端支持的最新版本
  - 如果双方无共同版本，返回错误
- [ ] 能力协商（Capability Negotiation）
  - 服务端能力声明：
    - `resources` (listChanged, subscribe)
    - `prompts` (listChanged)
    - `tools` (listChanged)
    - `logging`
    - `completions`
    - `experimental`
  - 根据配置动态启用/禁用能力
- [ ] 返回 `InitializeResult`
  ```json
  {
    "protocolVersion": "2025-06-18",
    "capabilities": { ... },
    "serverInfo": {
      "name": "string",
      "title": "string",
      "version": "string"
    },
    "instructions": "可选的使用说明"
  }
  ```
- [ ] 等待 `initialized` 通知
  - 在收到通知前，只能处理 ping 和 logging 请求
  - 超时处理
- [ ] 状态管理
  - `Uninitialized` → `Initializing` → `Initialized` → `Shutdown`

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle

---

### 4. 运行阶段

**优先级**: 🔴 最高

**目标**: 实现正常运行阶段的消息处理

**任务**:
- [ ] 实现请求路由器
  - 根据 `method` 字段分发请求
  - 方法名到处理器的映射
  - 未知方法返回 `-32601` 错误
- [ ] 实现响应管理器
  - 请求 ID 到 TaskCompletionSource 的映射
  - 超时管理（默认 30 秒可配置）
  - 响应与请求的关联
- [ ] 能力检查中间件
  - 验证请求的功能是否已在初始化时协商
  - 未协商的功能返回 `-32601` 错误
- [ ] 超时管理
  - 为每个发出的请求设置超时
  - 超时后发送 `notifications/cancelled`
  - 超时配置可按请求类型定制
- [ ] 进度通知支持
  - 检测请求中的 `_meta.progressToken`
  - 进度通知可能重置超时时钟（可配置）
  - 强制最大超时，不受进度通知影响
- [ ] 并发请求管理
  - 支持多个请求同时进行
  - 请求队列管理
  - 资源限制（最大并发请求数）

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle

---

### 5. 关闭阶段

**优先级**: 🟡 中等

**目标**: 实现优雅关闭机制

**任务**:
- [ ] 检测流关闭
  - 对于 stdio：检测 stdin 关闭
  - 对于其他流：检测 EndOfStream
- [ ] 优雅关闭流程
  - 停止接受新请求
  - 等待进行中的请求完成（有超时）
  - 取消所有待处理的请求
- [ ] 资源清理
  - 关闭所有订阅
  - 释放文件句柄
  - 清理缓存
- [ ] 通知应用层关闭事件
  - `OnShutdownStarting`
  - `OnShutdownCompleted`

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle

---

## 第三阶段：服务端核心功能

### 6. Resources（资源）

**优先级**: 🟠 高

**目标**: 实现资源暴露和访问功能

**任务**:
- [ ] `resources/list` 请求处理
  - 支持分页（cursor/nextCursor）
  - 返回资源列表
  - 调用 `IResourceProvider.ListResourcesAsync()`
- [ ] `resources/read` 请求处理
  - 根据 URI 读取资源
  - 支持文本内容（text）
  - 支持二进制内容（blob，base64 编码）
  - 调用 `IResourceProvider.ReadResourceAsync(uri)`
  - 资源不存在返回 `-32002` 错误
- [ ] `resources/templates/list` 请求处理
  - 返回 URI 模板列表
  - 支持 RFC 6570 URI 模板
  - 调用 `IResourceProvider.ListResourceTemplatesAsync()`
- [ ] `resources/subscribe` 请求处理（可选）
  - 订阅资源变化通知
  - 维护订阅列表
  - 调用 `IResourceProvider.SubscribeAsync(uri)`
- [ ] `notifications/resources/list_changed` 通知
  - 资源列表变化时发送
  - 需要在能力中声明 `listChanged`
- [ ] `notifications/resources/updated` 通知（可选）
  - 被订阅的资源更新时发送
  - 包含更新的资源 URI
- [ ] 数据类型实现
  - `Resource`：uri, name, title, description, mimeType, size
  - `ResourceContents`：uri, mimeType, text/blob
  - `ResourceTemplate`：uriTemplate, name, title, description, mimeType
- [ ] Annotations 支持
  - `audience`: ["user" | "assistant"]
  - `priority`: 0.0 - 1.0
  - `lastModified`: ISO 8601 时间戳
- [ ] URI 验证和处理
  - 支持常见 URI scheme：`file://`, `https://`, `git://`
  - 自定义 URI scheme 支持
  - URI 格式验证（RFC 3986）
- [ ] 错误处理
  - 资源不存在：`-32002`
  - URI 无效：`-32602`

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/server/resources

---

### 7. Prompts（提示模板）

**优先级**: 🟠 高

**目标**: 实现提示模板功能

**任务**:
- [ ] `prompts/list` 请求处理
  - 支持分页
  - 返回提示列表
  - 调用 `IPromptProvider.ListPromptsAsync()`
- [ ] `prompts/get` 请求处理
  - 根据名称获取提示
  - 支持参数化（arguments）
  - 参数验证（required 字段）
  - 调用 `IPromptProvider.GetPromptAsync(name, arguments)`
  - 提示不存在返回 `-32602` 错误
- [ ] `notifications/prompts/list_changed` 通知
  - 提示列表变化时发送
- [ ] 数据类型实现
  - `Prompt`：name, title, description, arguments
  - `PromptArgument`：name, description, required
  - `PromptMessage`：role (user/assistant), content
- [ ] 内容类型支持
  - `TextContent`：type="text", text
  - `ImageContent`：type="image", data (base64), mimeType
  - `AudioContent`：type="audio", data (base64), mimeType
  - `EmbeddedResource`：type="resource", resource
- [ ] Annotations 支持
  - 同 Resources
- [ ] 参数验证
  - 检查 required 参数是否提供
  - 参数类型验证（如果定义了 schema）
- [ ] 错误处理
  - 提示不存在：`-32602`
  - 缺少必需参数：`-32602`

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/server/prompts

---

### 8. Tools（工具）

**优先级**: 🟠 高

**目标**: 实现工具调用功能

**任务**:
- [ ] `tools/list` 请求处理
  - 支持分页
  - 返回工具列表
  - 调用 `IToolProvider.ListToolsAsync()`
- [ ] `tools/call` 请求处理
  - 根据名称调用工具
  - 参数验证（inputSchema）
  - 调用 `IToolProvider.CallToolAsync(name, arguments)`
  - 工具不存在返回 `-32602` 错误
  - 支持长时间运行的工具（进度通知）
- [ ] `notifications/tools/list_changed` 通知
  - 工具列表变化时发送
- [ ] 数据类型实现
  - `Tool`：name, title, description, inputSchema, outputSchema, annotations
  - `ToolResult`：content, structuredContent, isError
- [ ] 输入验证
  - 使用 JSON Schema 验证 inputSchema
  - 参数类型检查
- [ ] 输出验证（可选）
  - 使用 JSON Schema 验证 outputSchema
  - 确保 structuredContent 符合 schema
- [ ] 工具结果内容类型
  - `TextContent`
  - `ImageContent`
  - `AudioContent`
  - `ResourceLink`：type="resource_link", uri, name, description, mimeType
  - `EmbeddedResource`：type="resource", resource
  - `StructuredContent`：在 structuredContent 字段返回 JSON 对象
- [ ] 工具执行错误处理
  - 协议错误：未知工具、无效参数等，返回 JSON-RPC error
  - 执行错误：工具执行失败，返回 `isError: true` 的 ToolResult
- [ ] Annotations 支持
  - 工具级别 annotations
  - 结果内容级别 annotations

**安全考虑**:
- [ ] 输入验证和清理
- [ ] 访问控制
- [ ] 速率限制
- [ ] 输出清理（防止信息泄露）

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/server/tools

---

## 第四阶段：工具功能

### 9. Logging（日志）

**优先级**: 🟡 中等

**目标**: 实现结构化日志功能

**任务**:
- [ ] `logging/setLevel` 请求处理
  - 设置最低日志级别
  - 支持的级别：debug, info, notice, warning, error, critical, alert, emergency
  - 保存日志级别配置
- [ ] `notifications/message` 通知发送
  - 发送日志消息
  - 包含 level, logger（可选）, data
  - 根据设置的最低级别过滤
- [ ] 日志级别实现（RFC 5424）
  - `debug`: 详细调试信息
  - `info`: 一般信息
  - `notice`: 正常但重要的事件
  - `warning`: 警告条件
  - `error`: 错误条件
  - `critical`: 严重条件
  - `alert`: 必须立即采取行动
  - `emergency`: 系统不可用
- [ ] 速率限制
  - 防止日志洪泛
  - 可配置的速率限制策略
- [ ] 敏感信息过滤
  - 自动移除密码、令牌等
  - 可配置的敏感字段列表
- [ ] 日志上下文
  - logger 名称（可选）
  - 任意 JSON 可序列化的 data

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/server/utilities/logging

---

### 10. Completion（参数自动补全）

**优先级**: 🟢 低

**目标**: 实现参数自动补全功能

**任务**:
- [ ] `completion/complete` 请求处理
  - 解析引用类型（ref/prompt 或 ref/resource）
  - 解析参数名称和当前值
  - 解析上下文（已解析的参数）
  - 调用 `ICompletionProvider.GetCompletionsAsync()`
- [ ] 引用类型支持
  - `ref/prompt`：引用提示，包含 name
  - `ref/resource`：引用资源，包含 uri
- [ ] 数据类型实现
  - `CompleteRequest`：ref, argument (name, value), context (arguments)
  - `CompleteResult`：values (最多 100), total, hasMore
- [ ] 补全逻辑
  - 根据当前输入提供建议
  - 模糊匹配支持
  - 按相关性排序
  - 考虑上下文参数
- [ ] 速率限制
  - 防止过于频繁的补全请求
  - 去抖动（客户端应该实现，但服务端也可提供保护）
- [ ] 错误处理
  - 能力未协商：`-32601`
  - 无效的提示/资源名称：`-32602`

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/server/utilities/completion

---

### 11. Pagination（分页）

**优先级**: 🟠 高

**目标**: 实现通用分页支持

**任务**:
- [ ] 游标实现
  - 不透明游标（opaque cursor）字符串
  - 游标生成（可以是 base64 编码的 JSON）
  - 游标解析和验证
  - 游标应该稳定但不需要跨会话持久化
- [ ] nextCursor 字段处理
  - 有更多结果时返回 nextCursor
  - 最后一页不返回 nextCursor
- [ ] 支持的操作
  - `resources/list`
  - `resources/templates/list`
  - `prompts/list`
  - `tools/list`
- [ ] 错误处理
  - 无效游标：`-32602`
  - 过期游标：`-32602`
- [ ] 分页策略
  - 可配置的页面大小
  - 游标过期策略（基于时间或会话）

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/server/utilities/pagination

---

### 12. Ping（连接检测）

**优先级**: 🟡 中等

**目标**: 实现连接健康检查

**任务**:
- [ ] `ping` 请求处理
  - 立即返回空响应 `{}`
  - 不执行任何业务逻辑
- [ ] 主动 Ping 支持
  - 服务端也可以向客户端发送 ping
  - 可配置的 ping 间隔
  - ping 超时检测
- [ ] 超时配置
  - 可配置的 ping 超时时间
  - 超时后的处理策略（记录日志、关闭连接等）
- [ ] 连接健康监控
  - 连续失败 ping 次数统计
  - 达到阈值时触发重连或关闭

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/basic/utilities/ping

---

### 13. Cancellation（请求取消）

**优先级**: 🟡 中等

**目标**: 实现请求取消功能

**任务**:
- [ ] `notifications/cancelled` 通知处理
  - 解析 requestId 和 reason
  - 查找对应的进行中的请求
  - 取消请求执行
  - 释放相关资源
- [ ] 取消令牌传播
  - 将取消信号传播到 IResourceProvider 等
  - 使用 CancellationTokenSource
  - 链接多个取消源
- [ ] 竞态条件处理
  - 请求已完成但取消通知到达
  - 忽略未知的 requestId
  - 不为被取消的请求发送响应
- [ ] 约束
  - `initialize` 请求不可取消
  - 只能取消本方发出的请求
- [ ] 超时与取消的集成
  - 超时后自动发送取消通知
  - 接收到取消通知后停止等待响应

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/basic/utilities/cancellation

---

### 14. Progress（进度通知）

**优先级**: 🟢 低

**目标**: 实现进度通知功能

**任务**:
- [ ] 检测请求中的 progressToken
  - 从 `_meta.progressToken` 提取
  - token 必须是 string 或 number
  - token 在活动请求中唯一
- [ ] `notifications/progress` 通知发送
  - 包含 progressToken, progress, total（可选）, message（可选）
  - progress 值必须递增
  - progress 和 total 可以是浮点数
- [ ] 进度跟踪
  - 维护活动的 progress token
  - 请求完成后清理 token
- [ ] 速率限制
  - 防止过于频繁的进度更新
  - 可配置的最小更新间隔
- [ ] 与超时的集成
  - 进度通知可能重置超时时钟（可配置）
  - 但仍然强制最大超时

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/basic/utilities/progress

---

### 15. _meta 字段处理

**优先级**: 🟡 中等

**目标**: 实现元数据支持

**任务**:
- [ ] `_meta` 字段保留和传递
  - 在所有消息类型中支持 `_meta`
  - 保留但不处理未知的 _meta 字段
- [ ] `progressToken` 提取
  - 从 `_meta.progressToken` 提取进度令牌
  - 验证 token 类型（string | number）
- [ ] 自定义元数据传递
  - 允许应用层添加自定义 _meta 字段
  - 透明传递客户端的 _meta 字段
- [ ] MCP 保留键名验证
  - 检查是否使用了保留前缀：
    - `modelcontextprotocol.*`
    - `mcp.*`
    - 以及其他保留模式
  - 拒绝使用保留键名（可选，或仅警告）
- [ ] 键名格式验证
  - 验证 prefix/name 结构
  - prefix: `label(.label)*/`
  - label: 字母开头，字母/数字结尾，中间可以有连字符
  - name: 字母数字开头和结尾，中间可以有 `-`, `_`, `.`

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/basic

---

## 第五阶段：错误处理和传输

### 16. 标准错误码

**优先级**: 🟠 高

**目标**: 实现完整的错误处理

**任务**:
- [ ] JSON-RPC 标准错误码
  - `-32700`: Parse error（JSON 解析失败）
  - `-32600`: Invalid Request（无效的请求对象）
  - `-32601`: Method not found（方法不存在）
  - `-32602`: Invalid params（无效的参数）
  - `-32603`: Internal error（内部错误）
- [ ] MCP 特定错误码
  - `-32002`: Resource not found（资源不存在）
- [ ] 错误响应结构
  - `code`: number
  - `message`: string
  - `data`: any（可选，包含详细错误信息）
- [ ] 错误日志记录
  - 所有错误都应记录到日志
  - 包含请求上下文
  - 不在日志中暴露敏感信息
- [ ] 用户友好的错误消息
  - 提供清晰的错误说明
  - 包含可操作的建议（如果适用）
  - 国际化支持（可选）

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/basic

---

### 17. stdio 传输层实现

**优先级**: 🟠 高

**目标**: 基于 Stream 实现 stdio 传输

**任务**:
- [ ] StdioMcpStream 实现
  - 包装 `Console.OpenStandardInput()` 为读取流
  - 包装 `Console.OpenStandardOutput()` 为写入流
  - 实现 `IMcpStream` 接口
- [ ] stdout 独占性确保
  - 确保只有 MCP 消息写入 stdout
  - 应用层日志重定向到 stderr
  - 调试输出重定向到 stderr
- [ ] stderr 日志输出（可选）
  - 将服务端日志输出到 stderr
  - UTF-8 编码
  - 客户端可以捕获、转发或忽略
- [ ] 流关闭处理
  - 检测 stdin 关闭
  - 触发优雅关闭流程
- [ ] 进程终止
  - stdin 关闭后等待一段时间
  - 如果进程未退出，响应 SIGTERM
  - 如果仍未退出，响应 SIGKILL

**参考文档**: 
- https://modelcontextprotocol.io/specification/2025-06-18/basic/transports

---

## 第六阶段：测试和 API 设计

### 18. 测试基础设施

**优先级**: 🟠 高

**目标**: 建立完整的测试框架

**任务**:
- [ ] 单元测试框架设置
  - 使用 xUnit / NUnit / MSTest
  - 测试项目结构
- [ ] 模拟流实现
  - `MemoryMcpStream`：基于内存的双向流
  - 方便单元测试，无需真实 IO
- [ ] 消息序列化测试
  - 所有消息类型的序列化/反序列化
  - 边界情况测试
  - 无效 JSON 处理
- [ ] 协议流程测试
  - 完整的初始化流程
  - 请求/响应往返
  - 通知发送
  - 错误场景
- [ ] 集成测试
  - 使用真实的 stdio 流
  - 端到端场景测试
- [ ] 错误场景测试
  - 网络中断
  - 无效消息
  - 超时
  - 并发冲突
- [ ] 性能测试
  - 高并发请求
  - 大消息处理
  - 内存泄漏检测

---

### 19. 服务端构建器 API

**优先级**: 🟠 高

**目标**: 设计易用的服务端 API

**任务**:
- [ ] `McpServerBuilder` 类
  - 流式配置 API
  - 链式调用支持
- [ ] 能力配置方法
  - `WithResources(IResourceProvider, options)`
  - `WithPrompts(IPromptProvider, options)`
  - `WithTools(IToolProvider, options)`
  - `WithLogging(options)`
  - `WithCompletions(ICompletionProvider)`
- [ ] 服务端信息配置
  - `WithServerInfo(name, title, version)`
  - `WithInstructions(instructions)`
- [ ] 传输配置
  - `UseStdio()`
  - `UseStream(IMcpStream)`
  - 未来：`UseHttp(options)`, `UseIpc(options)`
- [ ] 选项配置
  - 超时配置
  - 并发限制
  - 日志级别
  - 分页大小
- [ ] Build 方法
  - `Build()` 返回 `IMcpServer`
  - 验证配置完整性
  - 构建内部组件
- [ ] 使用示例
  ```csharp
  var server = new McpServerBuilder()
      .WithServerInfo("my-server", "My Server", "1.0.0")
      .WithResources(new MyResourceProvider())
      .WithTools(new MyToolProvider())
      .UseStdio()
      .Build();
  
  await server.RunAsync(cancellationToken);
  ```

---

### 20. 资源/提示/工具提供者接口

**优先级**: 🟠 高

**目标**: 定义清晰的扩展点接口

**任务**:
- [ ] `IResourceProvider` 接口
  ```csharp
  interface IResourceProvider
  {
      Task<PaginatedList<Resource>> ListResourcesAsync(string? cursor, CancellationToken ct);
      Task<ResourceContents> ReadResourceAsync(string uri, CancellationToken ct);
      Task<List<ResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct);
      
      // 可选
      Task SubscribeAsync(string uri, CancellationToken ct);
      Task UnsubscribeAsync(string uri, CancellationToken ct);
  }
  ```
- [ ] `IPromptProvider` 接口
  ```csharp
  interface IPromptProvider
  {
      Task<PaginatedList<Prompt>> ListPromptsAsync(string? cursor, CancellationToken ct);
      Task<PromptResult> GetPromptAsync(string name, Dictionary<string, object>? arguments, CancellationToken ct);
  }
  ```
- [ ] `IToolProvider` 接口
  ```csharp
  interface IToolProvider
  {
      Task<PaginatedList<Tool>> ListToolsAsync(string? cursor, CancellationToken ct);
      Task<ToolResult> CallToolAsync(string name, Dictionary<string, object>? arguments, IProgress<ProgressUpdate>? progress, CancellationToken ct);
  }
  ```
- [ ] `ICompletionProvider` 接口
  ```csharp
  interface ICompletionProvider
  {
      Task<CompletionResult> GetCompletionsAsync(CompletionRequest request, CancellationToken ct);
  }
  ```
- [ ] 所有接口的异步方法签名
- [ ] CancellationToken 支持
- [ ] IProgress<T> 支持（用于进度报告）
- [ ] 数据传输对象（DTO）定义
  - `Resource`, `Prompt`, `Tool`
  - `ResourceContents`, `PromptResult`, `ToolResult`
  - `PaginatedList<T>`
- [ ] 依赖注入友好设计
  - 接口可以通过 DI 容器注册
  - 支持作用域和单例生命周期

---

## 实现优先级总结

### 🔴 第一优先级（必须立即实现）
1. JSON-RPC 2.0 消息基础设施
2. MCP over Stream 基础架构
3. 初始化阶段
4. 运行阶段

### 🟠 第二优先级（核心功能）
5. Resources
6. Prompts
7. Tools
8. Pagination
9. 错误处理
10. stdio 传输
11. 测试基础设施
12. API 设计

### 🟡 第三优先级（增强功能）
13. Logging
14. Ping
15. Cancellation
16. 关闭阶段
17. _meta 字段处理

### 🟢 第四优先级（可选功能）
18. Completion
19. Progress

## 架构原则

1. **零依赖**: 不依赖任何第三方 NuGet 包（除了 .NET 运行时）
2. **流优先**: 以抽象流为基础，支持多种传输方式
3. **可测试**: 所有组件都可以单独测试
4. **可扩展**: 通过接口提供清晰的扩展点
5. **异步优先**: 所有 IO 操作都是异步的
6. **取消支持**: 所有长时间运行的操作都支持取消
7. **类型安全**: 使用强类型而非动态类型
8. **错误处理**: 完善的错误处理和报告机制

## 下一步行动

建议按照以下顺序开始实现：

1. ✅ 创建项目结构和基本类型
2. 🔄 实现 JSON-RPC 消息类型（任务 1）
3. 🔄 实现 Stream 传输层（任务 2）
4. 🔄 实现初始化流程（任务 3）
5. 🔄 实现请求路由和响应管理（任务 4）
6. 🔄 实现至少一个功能（建议从 Resources 开始，任务 6）
7. 🔄 添加测试（任务 18）
8. 🔄 完善 API 设计（任务 19-20）

每完成一个阶段，都应该：
- ✅ 编写单元测试
- ✅ 编写集成测试
- ✅ 更新文档
- ✅ Code Review

祝实现顺利！🚀
