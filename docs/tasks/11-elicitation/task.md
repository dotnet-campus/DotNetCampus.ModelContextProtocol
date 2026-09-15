# 11 - Elicitation support

## 目标

完整实现 form/url 两种 elicitation 模式，包括正确的数据模型、能力协商、服务端反向请求和 URL 完成通知。

## 前置任务

- 02 - Protocol error code mapping
- 07 - Server notifications and list_changed support

## 任务列表

- [ ] 修正 `ElicitResult` 为 `action` 与可选 `content`。
- [ ] 支持 accept、decline、cancel。
- [ ] Client builder 增加 form/url elicitation handler。
- [ ] 自动声明 `ClientCapabilities.Elicitation` 子能力。
- [ ] 服务端增加发起 `elicitation/create` 的 API。
- [ ] 实现 URL mode 的 `notifications/elicitation/complete`。
- [ ] 实现 `URL_ELICITATION_REQUIRED (-32042)` 错误及 data。
- [ ] 校验 form requestedSchema 的受限 JSON Schema。

## 完成标准

- Form 和 URL mode 均可端到端执行。
- 响应字段与官方 schema 一致，不再输出旧 `data` 字段。
- 未声明对应子能力时不会发送相关 elicitation 请求。

## 官方依据

- [Elicitation](https://modelcontextprotocol.io/specification/2025-11-25/client/elicitation)

## 当前代码证据

- `ElicitationCreate` 常量和部分 DTO 已存在：`src/DotNetCampus.ModelContextProtocol/Protocol/RequestMethods.cs:136`、`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Elicitation.cs`。
- 当前 `ElicitResult` 使用错误的 `data` 字段：`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Elicitation.cs:401-408`。
- 客户端反向请求分发只支持 sampling：`src/DotNetCampus.ModelContextProtocol/Transports/ClientTransportManager.cs:167-208`。

