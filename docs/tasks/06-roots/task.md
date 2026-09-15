# 06 - Roots support

## 目标

实现服务器到客户端的 `roots/list` 反向请求，以及客户端 roots provider 和 roots 变化通知。

## 任务列表

- [ ] McpClientBuilder 增加 roots provider/handler。
- [ ] 自动声明 `ClientCapabilities.Roots`。
- [ ] ClientTransportManager 处理 `roots/list`。
- [ ] 服务端增加 `ListRootsAsync` 反向请求 API。
- [ ] 客户端 roots 变化时发送 `notifications/roots/list_changed`。
- [ ] 服务端接收通知后可重新拉取 roots。
- [ ] 校验 root URI 和可选 name。

## 完成标准

- 服务端可通过所有双向传输层获取客户端 roots。
- 未声明 roots 能力时，服务端 API 能明确报告不支持。
- roots 变化通知和重新拉取流程有端到端测试。

## 官方依据

- [Roots](https://modelcontextprotocol.io/specification/2025-11-25/client/roots)

## 当前代码证据

- Roots DTO 已存在：`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Roots.cs`。
- `ClientCapabilities.Roots` 已存在：`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/ClientCapabilities.cs:22`。
- ClientTransportManager 当前只处理 sampling 反向请求：`src/DotNetCampus.ModelContextProtocol/Transports/ClientTransportManager.cs:167`。
- McpClientBuilder 尚无 roots provider。

