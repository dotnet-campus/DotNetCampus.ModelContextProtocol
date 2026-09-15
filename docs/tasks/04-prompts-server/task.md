# 04 - Prompts server support

## 目标

实现服务器端 Prompts 能力，使现有客户端 `ListPromptsAsync` 和 `GetPromptAsync` 能与本库服务器完整互操作。

## 任务列表

- [ ] 设计并实现 prompt provider 和公开注册 API。
- [ ] 支持静态 prompt 和带 arguments 的 prompt template。
- [ ] 实现 `prompts/list` handler 和协议路由。
- [ ] 实现 `prompts/get` handler 和参数校验。
- [ ] 在 initialize 中按实际情况声明 `prompts` capability。
- [ ] 添加服务端、客户端和各传输层的端到端测试。
- [ ] list_changed 通知留给任务 07 接入。

## 完成标准

- 本库客户端可以列出并获取本库服务器注册的 prompt。
- 不存在的 prompt 和缺失参数返回明确的协议错误。
- 没有注册 prompt 时不声明 prompts capability。

## 官方依据

- [Prompts](https://modelcontextprotocol.io/specification/2025-11-25/server/prompts)

## 当前代码证据

- Prompt DTO 和客户端 API 已存在：`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Prompts.cs`、`src/DotNetCampus.ModelContextProtocol/Clients/McpClient.cs:186-214`。
- 服务端初始化固定 `Prompts = null`：`src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:103`。
- 服务端桥没有 prompts 路由：`src/DotNetCampus.ModelContextProtocol/Servers/McpProtocolBridge.cs:123-144`。
- 现有测试明确标记服务端待实现：`tests/DotNetCampus.ModelContextProtocol.Tests/Clients/CoreTests.cs:350-357`。

