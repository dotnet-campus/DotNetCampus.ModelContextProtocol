# 02 - Protocol error code mapping

## 目标

让已实现功能返回符合 MCP/JSON-RPC 规范的错误码和错误数据，避免把可识别的协议错误统一降级为 `InternalError`。

## 任务列表

- [ ] 盘点官方通用错误和 feature-specific errors。
- [ ] 为资源不存在、无效参数、方法不支持等场景建立明确映射。
- [ ] 接入 `ResourceNotFound = -32002`。
- [ ] 为后续 URL elicitation 接入 `UrlElicitationRequired = -32042` 的错误结构。
- [ ] 统一 `McpServerException` 到 `JsonRpcError` 的转换入口。
- [ ] 增加错误码、message 和 data 的 golden tests。

## 完成标准

- 已知协议错误不再错误地返回 `-32603 InternalError`。
- 错误响应包含正确的 request id、code、message 和规范要求的 data。
- 现有 tools/resources 请求的错误路径有测试覆盖。

## 官方依据

- [Basic protocol](https://modelcontextprotocol.io/specification/2025-11-25/basic)
- [Elicitation errors](https://modelcontextprotocol.io/specification/2025-11-25/client/elicitation#error-handling)

## 当前代码证据

- 错误码已经定义：`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/JsonRpc/JsonRpcErrorCode.cs:55-61`。
- 资源不存在时未设置协议错误码：`src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:531-546`。
- 未指定错误码的 `McpServerException` 被映射为 InternalError：`src/DotNetCampus.ModelContextProtocol/Servers/McpProtocolBridge.cs:195`。

