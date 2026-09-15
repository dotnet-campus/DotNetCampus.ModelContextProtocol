# 09 - Sampling capability negotiation

## 目标

在现有 sampling 基础上补齐 context 和 tools 子能力协商，避免向未声明能力的客户端发送不合规请求。

## 任务列表

- [ ] Client builder 可独立声明 sampling.context。
- [ ] Client builder 可独立声明 sampling.tools。
- [ ] 服务端校验 `includeContext` 与 client capability。
- [ ] 服务端校验 `tools` / `toolChoice` 与 client capability。
- [ ] 客户端 handler 支持 sampling tool use/tool result 循环所需内容。
- [ ] 为不支持、用户拒绝和 handler 错误建立明确错误路径。
- [ ] 增加 capability matrix 测试。

## 完成标准

- 服务端不会向仅声明基础 sampling 的客户端发送 tools/context 扩展请求。
- sampling tools 的 content blocks 能正确序列化和反序列化。
- capability 组合均有成功与拒绝测试。

## 官方依据

- [Sampling](https://modelcontextprotocol.io/specification/2025-11-25/client/sampling)

## 当前代码证据

- `SamplingCapability` 已有 Context/Tools：`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/ClientCapabilities.cs:59-71`。
- `McpServerSampling.IsSupported` 只判断 Sampling 是否非空：`src/DotNetCampus.ModelContextProtocol/Servers/McpServerSampling.cs:86`。
- `WithSamplingHandler` 只声明空 SamplingCapability：`src/DotNetCampus.ModelContextProtocol/Clients/McpClientBuilder.cs:169`。

