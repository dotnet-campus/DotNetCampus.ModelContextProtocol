# 使用 MCP Inspector 调试示例服务器

本文档介绍如何使用 MCP Inspector 调试示例 MCP 服务器。

## 前置要求

1. 安装 Node.js (v18 或更高版本)
2. 安装 .NET SDK (用于运行示例服务器)

## 步骤 1: 启动示例服务器

在项目根目录下运行：

```powershell
dotnet run --project samples/DotNetCampus.SampleMcpServer/DotNetCampus.SampleMcpServer.csproj
```

服务器将在 `http://localhost:5942/` 上监听。

## 步骤 2: 启动 MCP Inspector

在另一个终端中运行：

```powershell
npx @modelcontextprotocol/inspector http://localhost:5942/
```

这将启动 MCP Inspector 并自动在浏览器中打开调试界面。

## 步骤 3: 测试连接

在 MCP Inspector 界面中：

1. Inspector 会自动发送 `initialize` 请求
2. 你应该能看到服务器返回的能力声明
3. 可以尝试调用 `tools/list` 查看可用的工具
4. 可以尝试调用 `ping` 测试连接

## 已实现的功能

当前示例服务器实现了以下功能：

- ✅ `initialize` - 初始化连接并返回服务器能力
- ✅ `ping` - 健康检查
- ✅ `tools/list` - 列出可用工具（包含一个示例 `echo` 工具）
- ✅ `resources/list` - 列出可用资源（当前为空）
- ✅ `prompts/list` - 列出可用提示（当前为空）

## 架构说明

### HTTP + SSE 传输层

服务器使用 HTTP + Server-Sent Events (SSE) 作为传输层：

- **POST 请求**: 客户端发送 JSON-RPC 请求到服务器
- **GET 请求**: 建立 SSE 连接，用于服务器向客户端推送通知
- **CORS**: 已配置允许跨域请求，支持浏览器端的 Inspector

### 消息格式

所有消息都遵循 JSON-RPC 2.0 规范：

```json
// 请求
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": { ... }
}

// 响应
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": { ... }
}

// 错误响应
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32601,
    "message": "Method not found"
  }
}
```

## 下一步

参考 `docs/tasks/server-tasks.md` 了解完整的 MCP 服务端实现计划。

当前实现是一个最小可工作的示例，用于：
- 验证 HTTP + SSE 传输层的正确性
- 支持使用 MCP Inspector 进行调试
- 作为完整实现的基础框架
