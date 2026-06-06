# 08 - Resource subscriptions and pagination

## 目标

补齐 resources 订阅/更新机制，并为所有列表型接口实现 cursor pagination。

## 前置任务

- 04 - Prompts server support
- 07 - Server notifications and list_changed support

## 任务列表

- [ ] 实现 `resources/subscribe` 与 `resources/unsubscribe` 路由和 handler。
- [ ] 按 session 保存资源订阅关系。
- [ ] 资源变化时发送 `notifications/resources/updated`。
- [ ] tools/list、resources/list、resources/templates/list、prompts/list 支持 cursor。
- [ ] 正确生成和消费 `nextCursor`。
- [ ] initialize 按实际能力声明 resources.subscribe。
- [ ] 增加跨 session 隔离和取消订阅测试。

## 完成标准

- 只有已订阅 session 收到资源更新通知。
- 断开 session 后订阅状态被清理。
- 所有标准列表接口均能稳定翻页，且 cursor 不透明。

## 官方依据

- [Resources](https://modelcontextprotocol.io/specification/2025-11-25/server/resources)
- [Pagination](https://modelcontextprotocol.io/specification/2025-11-25/server/utilities/pagination)

## 当前代码证据

- subscribe/unsubscribe 常量已存在：`src/DotNetCampus.ModelContextProtocol/Protocol/RequestMethods.cs:58-66`。
- 服务端桥尚未路由订阅请求：`src/DotNetCampus.ModelContextProtocol/Servers/McpProtocolBridge.cs:123-144`。
- list handlers 当前忽略 cursor 并返回全量结果：`src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:209`、`:407`、`:449`。

