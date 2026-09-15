# 10 - Cancellation and progress

## 目标

实现双向请求取消和长任务进度通知，使请求生命周期与 MCP 通用 utilities 对齐。

## 前置任务

- 07 - Server notifications and list_changed support

## 任务列表

- [ ] 为进行中的入站请求维护 request id 到 CancellationTokenSource 的映射。
- [ ] 发送侧取消时发送 `notifications/cancelled`。
- [ ] 接收侧处理 `notifications/cancelled` 并取消对应 handler。
- [ ] 支持请求 `_meta.progressToken`。
- [ ] 提供发送和订阅 `notifications/progress` 的 API。
- [ ] 遵守 initialize request 不可取消等规范限制。
- [ ] 覆盖竞态、重复取消、请求已完成后取消等情况。

## 完成标准

- 取消能跨传输层传播到实际执行中的 handler。
- 被取消请求不会产生错误的后续成功响应。
- Progress token 与请求正确关联，且 progress 值满足单调递增要求。

## 官方依据

- [Cancellation](https://modelcontextprotocol.io/specification/2025-11-25/basic/utilities/cancellation)
- [Progress](https://modelcontextprotocol.io/specification/2025-11-25/basic/utilities/progress)

## 当前代码证据

- Cancelled/Progress DTO 已存在：`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Notifications.cs:12-116`。
- 服务端通知处理目前只识别 initialized：`src/DotNetCampus.ModelContextProtocol/Servers/McpProtocolBridge.cs:96-101`。
- 客户端无 id 消息当前直接返回：`src/DotNetCampus.ModelContextProtocol/Transports/ClientTransportManager.cs:155-161`。

