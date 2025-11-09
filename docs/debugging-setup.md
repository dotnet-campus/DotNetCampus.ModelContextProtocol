# MCP 服务器调试指南

## 概述

本项目现已支持使用 MCP Inspector 进行调试。我已对 `HttpTransport` 和示例服务器进行了轻微调整，使其能够与 `@modelcontextprotocol/inspector` 配合使用。

## 主要修改

### 1. HttpTransport.cs 的改进

**文件**: `src/DotNetCampus.ModelContextProtocol/Transports/HttpTransport.cs`

主要变更：
- ✅ 添加了 **Server-Sent Events (SSE)** 支持，用于服务端到客户端的推送通知
- ✅ 支持 **GET** 请求建立 SSE 连接
- ✅ 支持 **POST** 请求处理 JSON-RPC 消息
- ✅ 添加了 **CORS** 头支持，允许浏览器端的 MCP Inspector 访问
- ✅ 改进的错误处理和日志输出
- ✅ 连接管理和心跳机制

### 2. MCP 消息类型定义

**文件**: 
- `src/DotNetCampus.ModelContextProtocol/Messages/JsonRpcMessage.cs`
- `src/DotNetCampus.ModelContextProtocol/Messages/McpMessages.cs`

新增了完整的 MCP 协议消息类型：
- `JsonRpcRequest` - JSON-RPC 请求
- `JsonRpcResponse` - JSON-RPC 响应
- `JsonRpcNotification` - JSON-RPC 通知
- `JsonRpcError` - 错误信息
- `InitializeParams` / `InitializeResult` - 初始化消息
- `ServerCapabilities` / `ClientCapabilities` - 能力声明
- 各种能力相关的类型（Tools, Resources, Prompts）

### 3. 示例服务器实现

**文件**: `samples/DotNetCampus.SampleMcpServer/Program.cs`

实现了基本的 MCP 协议处理：
- ✅ `initialize` - 初始化并返回服务器能力
- ✅ `ping` - 健康检查
- ✅ `tools/list` - 返回可用工具列表（包含示例 echo 工具）
- ✅ `resources/list` - 返回资源列表
- ✅ `prompts/list` - 返回提示列表

## 使用方法

### 启动示例服务器

```powershell
# 在项目根目录运行
dotnet run --project samples/DotNetCampus.SampleMcpServer/DotNetCampus.SampleMcpServer.csproj
```

服务器将在 `http://localhost:5942/` 上监听。

### 使用 MCP Inspector 调试

在另一个终端中运行：

```powershell
npx @modelcontextprotocol/inspector http://localhost:5942/
```

Inspector 会：
1. 自动在浏览器中打开调试界面
2. 连接到你的 MCP 服务器
3. 自动发送 `initialize` 请求
4. 显示服务器的能力和可用功能

### 手动测试（可选）

如果想要手动测试，可以使用 PowerShell：

```powershell
# 测试 initialize 请求
Invoke-RestMethod -Uri "http://localhost:5942/" -Method POST `
  -Body '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test-client","version":"1.0.0"}}}' `
  -ContentType "application/json"

# 测试 tools/list 请求
Invoke-RestMethod -Uri "http://localhost:5942/" -Method POST `
  -Body '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' `
  -ContentType "application/json"
```

## 架构说明

### HTTP + SSE 双向通信

```
┌─────────────────┐                    ┌─────────────────┐
│  MCP Inspector  │                    │  MCP Server     │
│  (浏览器端)      │                    │  (.NET)         │
└─────────────────┘                    └─────────────────┘
         │                                      │
         │  GET / (建立 SSE 连接)               │
         │─────────────────────────────────────>│
         │                                      │
         │  event: open (连接成功)              │
         │<─────────────────────────────────────│
         │                                      │
         │  POST / (发送 JSON-RPC 请求)         │
         │─────────────────────────────────────>│
         │                                      │
         │  200 OK (JSON-RPC 响应)              │
         │<─────────────────────────────────────│
         │                                      │
         │  : heartbeat (SSE 心跳)              │
         │<─────────────────────────────────────│
         │                                      │
```

### 消息格式

所有消息遵循 JSON-RPC 2.0 规范：

```json
// 请求示例
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "2024-11-05",
    "capabilities": {},
    "clientInfo": {
      "name": "test-client",
      "version": "1.0.0"
    }
  }
}

// 响应示例
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "2024-11-05",
    "capabilities": {
      "tools": { "listChanged": true },
      "resources": { "listChanged": true, "subscribe": true },
      "prompts": { "listChanged": true }
    },
    "serverInfo": {
      "name": "DotNetCampus.SampleMcpServer",
      "version": "0.1.0"
    }
  }
}
```

## 当前实现状态

### ✅ 已实现
- HTTP + SSE 传输层
- CORS 支持
- 基本的 JSON-RPC 消息处理
- `initialize` 协议初始化
- `ping` 健康检查
- `tools/list`, `resources/list`, `prompts/list` 基础方法
- 与 MCP Inspector 的完整兼容性

### 🚧 待实现
- 完整的工具调用（`tools/call`）
- 资源读取（`resources/read`）
- 提示渲染（`prompts/get`）
- 进度通知
- 取消请求支持
- 更完整的错误处理

## 参考文档

- [MCP Inspector 使用指南](./mcp-inspector-guide.md)
- [测试指南](./testing-guide.md)
- [服务端实现任务](./tasks/server-tasks.md)
- [MCP 规范](https://modelcontextprotocol.io/specification/2025-06-18/basic)

## 下一步

参考 `docs/tasks/server-tasks.md` 了解完整的实现计划。当前的实现提供了：
1. 可工作的 HTTP + SSE 传输层
2. 与 MCP Inspector 的调试支持
3. 基础的协议框架，为完整实现打下基础

现在你可以使用 MCP Inspector 来探索和调试你的 MCP 服务器了！
