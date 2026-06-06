# 03 - Metadata, icons, and implementation information

## 目标

补齐最新规范中的显示元数据和 icons 支持，使 client/server implementation、tools、resources 和 prompts 能完整表达官方定义的信息。

## 任务列表

- [ ] Server builder 支持 title、description、icons、websiteUrl。
- [ ] Client builder 支持 title、description、icons、websiteUrl。
- [ ] initialize 请求和响应写入完整 `Implementation` 信息。
- [ ] Tool 注册和源生成器支持 icons。
- [ ] Resource 的 `IconSource` 实际生成到 `Resource.Icons`。
- [ ] 为后续 Prompt 注册提供 icons 配置。
- [ ] Tool 注册支持 `execution.taskSupport` 元数据，但本任务不实现 Tasks 行为。

## 完成标准

- initialize 中的 implementation metadata 可由公开 API 配置并正确序列化。
- tools/resources/prompts 的 icons 可配置、可列出并有序列化测试。
- 不配置新增元数据时保持兼容，不输出多余字段。

## 官方依据

- [2025-11-25 Changelog](https://modelcontextprotocol.io/specification/2025-11-25/changelog)
- [Lifecycle](https://modelcontextprotocol.io/specification/2025-11-25/basic/lifecycle)
- [Tools](https://modelcontextprotocol.io/specification/2025-11-25/server/tools)
- [Resources](https://modelcontextprotocol.io/specification/2025-11-25/server/resources)
- [Prompts](https://modelcontextprotocol.io/specification/2025-11-25/server/prompts)

## 当前代码证据

- `Implementation` 已定义新增字段：`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Implementation.cs:42-60`。
- 初始化当前只设置 Name/Version：`src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:89-94`。
- Resource model 已读取 `IconSource`，但 source builder 未写入 Icons：`src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/McpServerResourceGeneratingModel.cs:79`、`src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerResourceSourceBuilder.cs:21-31`。

