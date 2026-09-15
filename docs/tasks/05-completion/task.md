# 05 - Completion support

## 目标

实现 `completion/complete`，为 prompts 和 resource templates 的参数提供标准补全能力。

## 前置任务

- 04 - Prompts server support

## 任务列表

- [ ] 增加 completion provider 和公开注册 API。
- [ ] 实现 `completion/complete` 服务端 handler 与路由。
- [ ] 支持 `ref/prompt` 和 `ref/resource`。
- [ ] 支持 completion context 中的已解析参数。
- [ ] 客户端增加 `CompleteAsync`。
- [ ] initialize 按实际情况声明 `completions` capability。
- [ ] 覆盖 total、hasMore 和空结果等响应。

## 完成标准

- Prompt 和 ResourceTemplate 参数均可获得有序补全结果。
- 客户端能正确处理 values、total、hasMore。
- 未注册 completion provider 时不声明 capability，并返回规范错误。

## 官方依据

- [Completion](https://modelcontextprotocol.io/specification/2025-11-25/server/utilities/completion)

## 当前代码证据

- 方法常量和 DTO 已存在：`src/DotNetCampus.ModelContextProtocol/Protocol/RequestMethods.cs:107`、`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Completion.cs`。
- `ServerCapabilities.Completions` 已存在：`src/DotNetCampus.ModelContextProtocol/Protocol/Messages/ServerCapabilities.cs:52`。
- 服务端路由、handler 和客户端公开 API 尚未实现。

