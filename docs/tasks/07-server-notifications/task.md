# 07 - Server notifications and list_changed support

## 目标

建立通用的服务端通知发送能力，并接入 tools/resources/prompts 的 list changed notifications。

## 前置任务

- 04 - Prompts server support

## 任务列表

- [ ] `IServerTransportSession` 增加发送 notification 的能力。
- [ ] 各 server transport session 实现 notification 写出。
- [ ] 客户端分发并暴露服务端通知事件或 handler。
- [ ] Tools provider 变化时发送 `notifications/tools/list_changed`。
- [ ] Resources provider 变化时发送 `notifications/resources/list_changed`。
- [ ] Prompts provider 变化时发送 `notifications/prompts/list_changed`。
- [ ] initialize 中按动态能力设置 `listChanged`。

## 完成标准

- 服务端可以发送不需要响应的标准 JSON-RPC notification。
- 客户端不会把 notification 当作未知请求或直接丢弃。
- list_changed 仅在对应 capability 声明后发送。

## 官方依据

- [Tools](https://modelcontextprotocol.io/specification/2025-11-25/server/tools)
- [Resources](https://modelcontextprotocol.io/specification/2025-11-25/server/resources)
- [Prompts](https://modelcontextprotocol.io/specification/2025-11-25/server/prompts)

## 当前代码证据

- list changed notification DTO 已存在：`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Notifications.cs:133-194`。
- 初始化当前硬编码 `ListChanged = false`：`src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:98-101`。
- `IServerTransportSession` 只有 `SendRequestAsync`：`src/DotNetCampus.ModelContextProtocol/Transports/IServerTransportSession.cs:35-48`。

