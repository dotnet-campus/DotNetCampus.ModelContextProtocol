# MCP 2025-11-25 latest protocol implementation todo

> Scope: 以 MCP 官方站点当前的 `2025-11-25 (latest)` 协议规范为准。本文只做粗粒度缺口清单，后续每项需要再拆设计与测试用例。官方规范入口：[2025-11-25 Overview](https://modelcontextprotocol.io/specification/2025-11-25)。

## Baseline

- 当前库已经把协议版本常量更新到 `2025-11-25`，见 `src/DotNetCampus.ModelContextProtocol/Protocol/ProtocolVersion.cs:69`。
- 当前库已经有 tools、resources、sampling、elicitation、tasks、icons 等一部分协议模型，但大量模型尚未接入端到端协议行为。
- 官方说明 TypeScript schema 是协议的 source of truth，并要求 JSON Schema Draft 2020-12 兼容，见 [MCP Overview: protocol schema](https://modelcontextprotocol.io/specification/2025-11-25#schema)。

## Ordered task index

以下任务按基础性、依赖关系和预计实现难度排序。每个目录中的 `task.md` 描述目标、范围和验收标准；`plan.md` 留给执行该任务时单独制定方案。

1. [01 - Protocol schema conformance baseline](01-schema-conformance-baseline/task.md)
2. [02 - Protocol error code mapping](02-error-code-mapping/task.md)
3. [03 - Metadata, icons, and implementation information](03-metadata-and-icons/task.md)
4. [04 - Prompts server support](04-prompts-server/task.md)
5. [05 - Completion support](05-completion/task.md)
6. [06 - Roots support](06-roots/task.md)
7. [07 - Server notifications and list_changed support](07-server-notifications/task.md)
8. [08 - Resource subscriptions and pagination](08-resource-subscriptions-and-pagination/task.md)
9. [09 - Sampling capability negotiation](09-sampling-capability-negotiation/task.md)
10. [10 - Cancellation and progress](10-cancellation-and-progress/task.md)
11. [11 - Elicitation support](11-elicitation/task.md)
12. [12 - Streamable HTTP compliance and resumability](12-streamable-http-compliance/task.md)
13. [13 - Authorization and OAuth 2.1](13-authorization-oauth/task.md)
14. [14 - Tasks protocol](14-tasks/task.md)

## Gap audit

### 1. 建立官方 schema diff / conformance test 基线

- 官方依据：[MCP Overview](https://modelcontextprotocol.io/specification/2025-11-25#schema) 说明 TypeScript schema 是协议 source of truth。
- 代码证据：
  - 当前模型是手写/局部维护；`McpInternalJsonContext` 只列到 `ToolChoice`，见 `src/DotNetCampus.ModelContextProtocol/CompilerServices/McpJsonContext.cs:140-181`。
  - `rg 'JsonSerializable\(typeof\((Elicit|Complete|GetTask|CreateTask|CancelTask|ListTasks|Progress|Cancelled|Root|ToolUse|ToolResult)' src/DotNetCampus.ModelContextProtocol/CompilerServices/McpJsonContext.cs` 无匹配。
- 粗任务：
  - 拉取/固定官方 2025-11-25 schema，做协议 DTO 与序列化上下文的自动 diff。
  - 给所有请求参数、结果、通知、content block、metadata、error code 补齐序列化覆盖。
  - 补一组 golden JSON 读写测试，防止“模型存在但字段名不对”。

### 2. 修正 elicitation 模型并补齐端到端能力

- 官方依据：[Elicitation](https://modelcontextprotocol.io/specification/2025-11-25/client/elicitation) 定义 `elicitation/create`、form/url mode、`ElicitResult.action` 与 `content`，以及 URL mode 的 `notifications/elicitation/complete` 和 `URL_ELICITATION_REQUIRED`。
- 代码证据：
  - `RequestMethods.ElicitationCreate` 已存在，见 `src/DotNetCampus.ModelContextProtocol/Protocol/RequestMethods.cs:136`。
  - 当前 `ElicitResult` 仍使用 `data` 字段，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Elicitation.cs:401` 和 `:407`。
  - 客户端只处理 `sampling/createMessage`，其他服务端反向请求返回 `MethodNotFound`，见 `src/DotNetCampus.ModelContextProtocol/Transports/ClientTransportManager.cs:167` 和 `:208`。
  - `McpClientBuilder` 只有 `WithSamplingHandler`，没有 elicitation handler，见 `src/DotNetCampus.ModelContextProtocol/Clients/McpClientBuilder.cs:163`。
- 粗任务：
  - 修正 `ElicitResult` 为 `action`/`content`，支持 `accept`、`decline`、`cancel`。
  - 增加客户端 elicitation handler/builder/capability，支持 form 和 URL mode。
  - 增加服务端发起 elicitation 的 API，并处理 URL 完成通知与 `-32042` 错误。

### 3. 实现 Tasks 协议

- 官方依据：[Tasks](https://modelcontextprotocol.io/specification/2025-11-25/basic/utilities/tasks) 定义任务状态、`tasks/get`、`tasks/result`、`tasks/cancel`、`tasks/list`、任务增强请求和 `notifications/tasks/status`。
- 代码证据：
  - tasks 方法常量已存在，见 `src/DotNetCampus.ModelContextProtocol/Protocol/RequestMethods.cs:173-197`。
  - tasks 能力模型已存在，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Tasks.cs:156` 和 `:240`。
  - `CallToolRequestParams` 已继承 `TaskAugmentedRequestParams`，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/CallToolRequestParams.cs:10`。
  - 服务端初始化没有声明 `Capabilities.Tasks`，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:95-104`。
  - 服务端桥没有 tasks 路由，当前只路由 initialize/ping/logging/tools/resources，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpProtocolBridge.cs:123-144`。
- 粗任务：
  - 增加任务存储、生命周期、TTL、状态通知和结果保留机制。
  - 实现 `tasks/get`、`tasks/result`、`tasks/cancel`、`tasks/list`。
  - 工具调用、sampling、elicitation 接入 task augmentation。
  - 初始化能力协商准确声明 client/server tasks 能力。

### 4. 实现 Prompts 服务端能力

- 官方依据：[Prompts](https://modelcontextprotocol.io/specification/2025-11-25/server/prompts) 定义 `prompts/list`、`prompts/get`、prompt arguments、prompt list changed notification。
- 代码证据：
  - 客户端已有 `ListPromptsAsync` / `GetPromptAsync`，见 `src/DotNetCampus.ModelContextProtocol/Clients/McpClient.cs:186` 和 `:204`。
  - 服务端初始化明确 `Prompts = null`，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:103`。
  - 服务端桥没有 `PromptsList` / `PromptsGet` 路由，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpProtocolBridge.cs:123-144`。
  - 测试中已标记“Server 端 Prompts 功能待实现”，见 `tests/DotNetCampus.ModelContextProtocol.Tests/Clients/CoreTests.cs:350` 和 `:356`。
- 粗任务：
  - 增加 prompt provider/builder/source generator 或手动注册 API。
  - 实现 `prompts/list`、`prompts/get` 与参数替换。
  - 初始化声明 `prompts` 能力，并支持 `notifications/prompts/list_changed`。

### 5. 实现 Completion 能力

- 官方依据：[Completion](https://modelcontextprotocol.io/specification/2025-11-25/server/utilities/completion) 定义 `completion/complete`，用于 prompts 与 resource templates 参数补全。
- 代码证据：
  - 方法常量已存在，见 `src/DotNetCampus.ModelContextProtocol/Protocol/RequestMethods.cs:107`。
  - DTO 已存在，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Completion.cs:9` 和 `:125`。
  - `ServerCapabilities.Completions` 已存在，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/ServerCapabilities.cs:52`。
  - 服务端桥没有 `CompletionComplete` 路由，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpProtocolBridge.cs:123-144`。
- 粗任务：
  - 增加 completion provider 与 `completion/complete` handler。
  - 给 prompt/resource template 参数接入补全来源。
  - 客户端增加 `CompleteAsync` API。
  - 初始化能力中按实际能力声明 `completions`。

### 6. 实现 Roots 反向请求

- 官方依据：[Roots](https://modelcontextprotocol.io/specification/2025-11-25/client/roots) 定义客户端 roots 能力、`roots/list` 与 `notifications/roots/list_changed`。
- 代码证据：
  - roots DTO 已存在，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Roots.cs:17`、`:28`、`:42`。
  - `ClientCapabilities.Roots` 已存在，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/ClientCapabilities.cs:22`。
  - `ClientTransportManager.HandleServerRequestAsync` 只处理 sampling，见 `src/DotNetCampus.ModelContextProtocol/Transports/ClientTransportManager.cs:167`。
  - `McpClientBuilder` 没有 roots handler/builder，`rg 'WithRoot|ListRoots' src/DotNetCampus.ModelContextProtocol` 仅命中 DTO/常量。
- 粗任务：
  - 增加客户端 roots provider/builder，声明 roots 能力。
  - 服务端增加 `ListRootsAsync` 反向请求 API。
  - 处理 `notifications/roots/list_changed` 并允许服务端刷新 roots。

### 7. 补齐 Sampling tools / context 能力检查

- 官方依据：[Sampling](https://modelcontextprotocol.io/specification/2025-11-25/client/sampling) 允许 `includeContext` 和 sampling-time tool calls，但这些子能力需要客户端声明。
- 代码证据：
  - `SamplingCapability` 有 `Context` 和 `Tools` 字段，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/ClientCapabilities.cs:59-71`。
  - `McpServerSampling.IsSupported` 只判断 `Sampling is not null`，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpServerSampling.cs:86`。
  - `WithSamplingHandler` 只设置空 `SamplingCapability`，未声明 `context`/`tools`，见 `src/DotNetCampus.ModelContextProtocol/Clients/McpClientBuilder.cs:169`。
  - sampling request DTO 已有 `Tools` / `ToolChoice`，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Sampling.cs:53` 和 `:68`。
- 粗任务：
  - 服务器发起 sampling 前根据 client capability 校验 `includeContext`、`tools`、`toolChoice`。
  - 客户端 builder 允许显式声明 sampling context/tools 支持。
  - 客户端 sampling handler 支持返回/处理中间 tool_use、tool_result 内容块。

### 8. 实现取消与进度通知

- 官方依据：[Cancellation](https://modelcontextprotocol.io/specification/2025-11-25/basic/utilities/cancellation) 和 [Progress](https://modelcontextprotocol.io/specification/2025-11-25/basic/utilities/progress) 定义 `notifications/cancelled` 与 `notifications/progress`。
- 代码证据：
  - 通知模型已存在，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Notifications.cs:12` 和 `:68`。
  - 服务端通知处理只识别 `notifications/initialized`，其他通知只打 warn，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpProtocolBridge.cs:96-101`。
  - 客户端收到无 id 消息直接返回，不处理通知，见 `src/DotNetCampus.ModelContextProtocol/Transports/ClientTransportManager.cs:155-161`。
  - 目前取消只取消本地等待的 TCS，不向对端发送 `notifications/cancelled`，见 `src/DotNetCampus.ModelContextProtocol/Transports/ClientTransportManager.cs:51`。
- 粗任务：
  - 请求发送侧取消时发 `notifications/cancelled`。
  - 请求接收侧把取消通知映射到对应 handler 的 `CancellationToken`。
  - 支持 `_meta.progressToken` 与 `notifications/progress` 发送/接收。

### 9. 完成资源订阅、列表变更通知与分页

- 官方依据：[Resources](https://modelcontextprotocol.io/specification/2025-11-25/server/resources) 定义 `resources/subscribe`、`resources/unsubscribe`、`notifications/resources/updated` 与资源列表变化；[Pagination](https://modelcontextprotocol.io/specification/2025-11-25/server/utilities/pagination) 定义 cursor/nextCursor。
- 代码证据：
  - `ResourcesSubscribe` / `ResourcesUnsubscribe` 常量已存在，见 `src/DotNetCampus.ModelContextProtocol/Protocol/RequestMethods.cs:58` 和 `:66`。
  - 初始化硬编码 `Subscribe = false, ListChanged = false`，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:101`。
  - 服务端桥没有 subscribe/unsubscribe 路由，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpProtocolBridge.cs:123-144`。
  - list handler 忽略 cursor，直接返回全量 tools/resources/templates，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:209`、`:407`、`:449`。
- 粗任务：
  - 实现资源订阅表和更新通知。
  - tools/resources/prompts/resource templates 支持分页与 nextCursor。
  - 按实际能力声明 `listChanged` / `subscribe`。

### 10. 补齐 Streamable HTTP 严格合规与恢复能力

- 官方依据：[Transports](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports) 定义 Streamable HTTP 的 Accept/Content-Type、SSE、session、`Last-Event-ID` 恢复、Origin 校验和 DNS rebinding 防护。
- 代码证据：
  - TouchSocket 注释写明 POST Accept 必须同时包含 `application/json` 和 `text/event-stream`，但 `ValidateAccept` 实际用 OR，见 `src/DotNetCampus.ModelContextProtocol.TouchSocket.Http/Transports/TouchSocket/TouchSocketHttpServerTransport.cs:918-930`。
  - LocalHost HTTP 的 POST handler 从 `protocolVersion`/body 开始处理，未见 Content-Type/Accept 校验，见 `src/DotNetCampus.ModelContextProtocol/Transports/Http/LocalHostHttpServerTransport.cs:392-400`。
  - SSE 写出只有 `event: message` 和 `data:`，没有事件 `id:`，见 `src/DotNetCampus.ModelContextProtocol/Transports/Http/HttpServerTransportSession.cs:13` 和 `:97`。
  - `Last-Event-ID` 只在 TouchSocket CORS expose 中出现，没有恢复逻辑，见 `src/DotNetCampus.ModelContextProtocol.TouchSocket.Http/Transports/TouchSocket/TouchSocketHttpServerTransport.cs:995`。
  - HTTP server-initiated request 依赖当前 POST SSE stream；没有绑定流会抛异常，见 `src/DotNetCampus.ModelContextProtocol/Transports/Http/HttpServerTransportSession.cs:66-74`。
- 粗任务：
  - 两套 HTTP server 统一严格校验 Content-Type/Accept。
  - SSE event 增加 id，客户端解析 `id:`/`retry:`，服务端保存可重放事件。
  - 支持 `Last-Event-ID` 恢复。
  - 允许通过 GET SSE stream 发送服务端主动请求/通知，而不只依赖当前 POST。

### 11. 实现 Authorization / OAuth 2.1 支持

- 官方依据：[Authorization](https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization) 定义 MCP HTTP 的 OAuth/OIDC 发现、Protected Resource Metadata、resource indicator 等要求。
- 代码证据：
  - 项目文档明确“当前版本尚未提供内置 Authorization 支持”，见 `docs/zh-hans/Authorization.md:5` 和 `docs/en/Authorization.md:5`。
  - `rg 'OAuth|OIDC|WWW-Authenticate|Protected Resource|authorization_servers|resource_metadata' src` 无协议实现匹配。
- 粗任务：
  - 服务端支持 401/`WWW-Authenticate`、Protected Resource Metadata 和授权服务器发现。
  - 客户端支持 OAuth metadata 发现、token 获取/刷新、resource parameter。
  - HTTP transport options 提供标准认证扩展点和测试 fake auth server。

### 12. 补齐 metadata/icons/implementation 信息的公开 API 与生成器

- 官方依据：[Changelog 2025-11-25](https://modelcontextprotocol.io/specification/2025-11-25/changelog) 和各 server feature 页面新增 icons / display metadata；[Lifecycle](https://modelcontextprotocol.io/specification/2025-11-25/basic/lifecycle) 中 initialize 交换 implementation 信息。
- 代码证据：
  - `Implementation` 已有 `Description`、`Icons`、`WebsiteUrl`，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Implementation.cs:42`、`:50`、`:58`。
  - 初始化只设置 `Name` / `Version`，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:89-94`。
  - `Tool` / `Resource` / `Prompt` 有 `Icons` 字段，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Tool.cs:77`、`Resources.cs:56`、`Prompts.cs:57`。
  - `McpServerToolAttribute` 没有 icon/execution/taskSupport 属性，见 `src/DotNetCampus.ModelContextProtocol/CompilerServices/McpServerToolAttribute.cs:11-105`。
  - `McpServerResourceAttribute.IconSource` 已读取到 model，但 resource source builder 未写入 `Icons`，见 `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/McpServerResourceGeneratingModel.cs:79` 与 `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerResourceSourceBuilder.cs:21-31`。
- 粗任务：
  - Server/client builder 暴露 implementation title/description/icons/websiteUrl。
  - Tool/resource/prompt 注册与生成器支持 icons。
  - Tool generator 支持 `execution.taskSupport`。

### 13. 补齐协议级错误码和错误映射

- 官方依据：[Basic protocol](https://modelcontextprotocol.io/specification/2025-11-25/basic) 使用 JSON-RPC error；多个 feature 页面定义 feature-specific errors，例如 URL elicitation `-32042`。
- 代码证据：
  - `JsonRpcErrorCode` 已包含 `ResourceNotFound = -32002` 与 `UrlElicitationRequired = -32042`，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/JsonRpc/JsonRpcErrorCode.cs:55` 和 `:61`。
  - 资源不存在时抛普通 `McpServerException`，未设置 `JsonRpcErrorCode`，最终桥接为 InternalError，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:531-546` 与 `src/DotNetCampus.ModelContextProtocol/Servers/McpProtocolBridge.cs:195`。
- 粗任务：
  - 为资源不存在、无效参数、任务不存在、URL elicitation required 等场景映射规范错误码。
  - 错误 `data` 结构按对应 feature 规范补齐。
  - 增加错误响应 golden tests。

### 14. 补齐通知发送 API 与 list_changed 能力

- 官方依据：tools/resources/prompts 均有 list changed notification，见 [Tools](https://modelcontextprotocol.io/specification/2025-11-25/server/tools)、[Resources](https://modelcontextprotocol.io/specification/2025-11-25/server/resources)、[Prompts](https://modelcontextprotocol.io/specification/2025-11-25/server/prompts)。
- 代码证据：
  - 通知模型存在，见 `src/DotNetCampus.ModelContextProtocol/Protocol/Messages/Notifications.cs:133`、`:184`、`:194`。
  - 初始化硬编码 `Tools.ListChanged = false`、`Resources.ListChanged = false`，见 `src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:98-101`。
  - `IServerTransportSession` 只有 `SendRequestAsync`，没有通用 `SendNotificationAsync`，见 `src/DotNetCampus.ModelContextProtocol/Transports/IServerTransportSession.cs:35-48`。
- 粗任务：
  - Server session/transport manager 增加服务端到客户端通知 API。
  - tools/resources/prompts provider 在动态变化时发 list_changed。
  - 初始化能力按 provider 是否支持动态变化声明。

## Later audit

- 完成上述任务后，再逐项对照官方 `2025-11-25` schema 做字段级审计，特别是 `_meta`、cursor、annotations、icons、content block 多态、tool name validation、authorization metadata、HTTP status code 与 header 细节。
- 官方扩展、registry、MCP Apps、post-2025-11-25 SEP 不在本轮清单范围内；如果本库目标是“完整覆盖官方网站所有扩展”，需要另开清单。
