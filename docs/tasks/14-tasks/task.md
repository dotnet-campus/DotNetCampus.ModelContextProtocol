# 14 - Tasks protocol

## 目标

实现 MCP `2025-11-25` experimental Tasks 协议，并将工具调用、sampling 和 elicitation 接入统一的异步任务生命周期。

## 前置任务

- 07 - Server notifications and list_changed support
- 08 - Resource subscriptions and pagination
- 09 - Sampling capability negotiation
- 10 - Cancellation and progress
- 11 - Elicitation support
- 12 - Streamable HTTP compliance and resumability

## 任务列表

- [ ] 定义任务存储抽象、状态机和并发规则。
- [ ] 实现 TTL、pollInterval、结果保留和清理。
- [ ] 实现 `tasks/get`。
- [ ] 实现 `tasks/result` 长轮询/等待语义。
- [ ] 实现 `tasks/cancel`。
- [ ] 实现可选 `tasks/list` 和 cursor pagination。
- [ ] 实现 `notifications/tasks/status`。
- [ ] tools/call 接入 task augmentation 和 `execution.taskSupport`。
- [ ] sampling/createMessage 接入客户端 tasks capability。
- [ ] elicitation/create 接入客户端 tasks capability。
- [ ] initialize 准确协商 client/server tasks capabilities。
- [ ] 覆盖 session 断开、取消、过期、失败和结果恢复。

## 完成标准

- 同步请求与 task-augmented 请求可以并存。
- 任务状态只能按规范状态机转换。
- get/result/cancel/list/status notification 全部可端到端运行。
- 工具、sampling、elicitation 均遵守各自声明的 task support。
- 任务生命周期与取消、进度、HTTP 重连正确协作。

## 官方依据

- [Tasks](https://modelcontextprotocol.io/specification/2025-11-25/basic/utilities/tasks)

## 当前代码证据

- Tasks DTO、capability 和方法常量已存在：`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Tasks.cs`、`src/DotNetCampus.ModelContextProtocol/Protocol/RequestMethods.cs:173-197`。
- tools/call 已能携带 task metadata：`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/CallToolRequestParams.cs:10`。
- 服务端初始化和协议桥尚未接入 Tasks：`src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:95-104`、`src/DotNetCampus.ModelContextProtocol/Servers/McpProtocolBridge.cs:123-144`。

