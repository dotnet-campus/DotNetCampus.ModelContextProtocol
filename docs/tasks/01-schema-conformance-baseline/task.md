# 01 - Protocol schema conformance baseline

## 目标

建立 MCP `2025-11-25` 官方 schema 与本库协议模型之间的可重复校验基线。后续协议功能开发必须能够发现字段、枚举、多态类型和序列化上下文的漂移。

## 任务列表

- [ ] 固定官方 `2025-11-25` TypeScript schema 或等价的生成产物作为测试输入。
- [ ] 盘点本库所有 request、result、notification、capability、content block 和 error 类型。
- [ ] 补齐 `McpInternalJsonContext` 缺失的协议类型。
- [ ] 为关键消息建立 golden JSON 序列化/反序列化测试。
- [ ] 增加自动检查，发现官方 schema 中新增但本库未覆盖的类型或字段。
- [ ] 明确 JSON Schema Draft 2020-12 的兼容边界。

## 完成标准

- 官方 schema 与本库 DTO 的差异可以通过测试或工具稳定重现。
- 新增协议类型时，遗漏 JSON source generation 注册会导致测试失败。
- 至少覆盖 initialize、tools、resources、prompts、completion、roots、sampling、elicitation、tasks 和通用通知。

## 官方依据

- [MCP 2025-11-25 Overview](https://modelcontextprotocol.io/specification/2025-11-25)
- [Protocol schema](https://modelcontextprotocol.io/specification/2025-11-25#schema)

## 当前代码证据

- 当前协议版本已设为 `2025-11-25`：`src/DotNetCampus.ModelContextProtocol/Protocol/ProtocolVersion.cs:69`。
- `McpInternalJsonContext` 当前注册范围见 `src/DotNetCampus.ModelContextProtocol/CompilerServices/McpJsonContext.cs:140-181`。
- Elicit、Complete、Tasks、Progress、Cancelled、Roots 等类型尚未完整注册到内部 JSON context。

