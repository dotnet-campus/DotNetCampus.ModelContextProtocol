# DotNetCampus.ModelContextProtocol 开发指南

> ⚠️ **AI 助手注意事项**：
> 本文件用于存储 **每个会话都必须遵循** 的核心开发规范。
> 如果本文件内容超过 200 行，请将扩展内容（详细实现示例、完整代码片段、故障排查手册等）移至 `/docs` 文件夹，并在此保留关键要点和文档链接。

## 项目概述

DotNetCampus.ModelContextProtocol 是一个轻量级、零依赖的 .NET MCP (Model Context Protocol) 协议实现库。

### 核心特性

- 🚀 轻量级高性能（零外部依赖，AOT 兼容）
- 🔌 易于集成（无论项目使用了什么框架）

### 项目结构

```
src/DotNetCampus.ModelContextProtocol/
├── Servers/                     # MCP 服务器实现
│   ├── McpServer.cs            # 服务器主类
│   ├── HttpServerTransport.cs  # HTTP 传输层
│   └── McpServerBuilder.cs     # 服务器构建器
├── Clients/                     # MCP 客户端（规划中）
├── Protocol/                    # 协议定义和 JSON 序列化上下文
├── Messages/                    # 消息类型定义
└── Core/                        # 核心类型
```

## 核心开发原则

### 1. 代码质量

- ✅ **减少分配**：使用流式 JSON 序列化（`JsonSerializer.SerializeAsync`）避免字符串分配
- ✅ **异步优先**：所有 I/O 操作使用 `async/await`
- ✅ **资源管理**：正确使用 `CancellationToken` 和 `IAsyncDisposable`

### 2. 协议实现

- **严格遵循 MCP 规范**：详见 [/docs/knowledge/http-transport-guide.md](../docs/knowledge/http-transport-guide.md)
- **支持多版本协议**：
  - 代码主要以最新版本协议进行编写
  - 遇到需要兼容旧协议的部分，用 `Legacy` 命名相关代码并尽量减少代码量
- **协议消息类型规范**：详见 [/docs/knowledge/protocol-messages-guide.md](../docs/knowledge/protocol-messages-guide.md)
  - 所有 Protocol 消息类型必须添加中英双语注释
  - 英文注释必须使用 MCP 官方 Schema 原文
  - 当前使用协议版本：**2025-11-25**
  - Schema 文件：[schema.ts](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/schema/2025-11-25/schema.ts)

### 3. 源代码生成

- **使用 SourceTextBuilder**：源生成器代码使用 `DotNetCampus.CodeAnalysisUtils` 库的 `SourceTextBuilder` 进行链式代码生成
- **优先链式调用**：使用 LINQ + `AddRawStatements` 等批量 API，避免循环中的单个调用
- **详细文档**：参见 [SourceTextBuilder API 使用指南](../docs/knowledge/sourcetextbuilder-guide.md)

## 快速参考

### HTTP 端点

| 协议版本 | 端点            | 方法   | 用途                                      |
| -------- | --------------- | ------ | ----------------------------------------- |
| 新协议   | `/mcp`          | POST   | 处理所有 JSON-RPC 请求                    |
| 新协议   | `/mcp`          | GET    | 建立 SSE 连接（不发送 `endpoint` 事件）   |
| 新协议   | `/mcp`          | DELETE | 终止会话（需 `Mcp-Session-Id` header）    |
| 旧协议   | `/mcp/sse`      | GET    | 建立 SSE 连接（必须发送 `endpoint` 事件） |
| 旧协议   | `/mcp/messages` | POST   | 处理消息（需 `sessionId` query 参数）     |

## 扩展文档

详细的实现指南、完整代码示例和故障排查手册请参阅：

- 📘 [HTTP 传输层开发指南](../docs/knowledge/http-transport-guide.md) - 完整协议实现细节
- 📋 [Protocol 消息类型注释规范](../docs/knowledge/protocol-messages-guide.md) - 消息类型双语注释标准和协议更新流程
- 🔧 [SourceTextBuilder API 使用指南](../docs/knowledge/sourcetextbuilder-guide.md) - 源代码生成器 API 详解

## 参考资源

- [MCP 官方规范 (2025-06-18)](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) - **当前使用版本**
- [MCP Schema (2025-06-18)](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/schema/2025-06-18/schema.ts) - **官方消息类型定义**
- [MCP 官方规范 (2024-11-05)](https://modelcontextprotocol.io/specification/2024-11-05/basic/transports) - 旧版本（兼容支持）
- [JSON-RPC 2.0 规范](https://www.jsonrpc.org/specification)
- [SSE 标准](https://html.spec.whatwg.org/multipage/server-sent-events.html)

## AI 助手特别提醒

### 更新 Protocol 消息时

1. **必须阅读** [Protocol 消息类型注释规范](../docs/knowledge/protocol-messages-guide.md)
2. **检查协议版本**：确认是否有新的 MCP 协议版本发布
3. **注释格式**：
   - 中文在前，使用中文标点（，（）。），末尾 `<br/>`
   - 英文在后，**必须使用官方 Schema 原文**
4. **命名约定**：
   - 请求参数：`{功能}RequestParams`
   - 响应结果：`{功能}Result`
   - 继承正确的基类（`RequestParams`/`Result`/`PaginatedRequestParams`/`PaginatedResult`）
5. **验证**：确保代码无编译错误，注释格式符合规范
