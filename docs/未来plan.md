# 协议兼容与版本协商设计方案

## 目标

在本库同时兼容以下四个 MCP 协议版本，并且让服务端与客户端都能在 `initialize` 阶段完成明确、可追踪的版本协商：

- 2025-11-25
- 2025-06-18
- 2025-03-26
- 2024-11-05

这里的“兼容”不应只停留在常量或头部校验，而应覆盖以下三个层面：

1. 生命周期兼容：`initialize` 的版本选择、会话绑定、后续请求校验。
2. 传输兼容：2025-03-26 及以上的 Streamable HTTP，与 2024-11-05 的旧版 HTTP+SSE 双栈支持。
3. 消息兼容：按协商后的协议版本裁剪能力、字段和传输行为，而不是默认总发最新模型。

## 结论先行

结合官方规范与当前仓库实现，最稳妥的方案不是在每个请求里临时判断版本，而是采用“内部统一用最新协议模型，边界层按协商版本做投影”的设计：

1. 内部协议处理仍以当前主版本 `2025-11-25` 的消息模型和处理器为主。
2. 在传输层会话上增加“已协商协议版本”和“传输族别”状态，整个会话期间固定使用。
3. 在 `initialize` 阶段引入统一的版本选择器，负责从客户端请求版本与服务器支持矩阵中选出最终版本，或返回标准错误。
4. 在 HTTP 入口层同时支持两类协议族：
	- Streamable HTTP：`/mcp`
	- Legacy HTTP+SSE：`/mcp/sse` 与 `/mcp/messages`
5. 对外发送消息前，根据协商版本做字段裁剪和行为约束；对内接收消息后，必要时做归一化。

这套设计的优点是：

- 对现有 `McpProtocolBridge`、`McpServerRequestHandlers`、工具/资源处理逻辑侵入最小。
- 能优先覆盖两套 HTTP 传输共有的协商与校验逻辑，避免同一套兼容规则写两遍；对于 2024-11-05 旧版 SSE，可先在 LocalHost 完整落地。
- 可以渐进式落地，先把版本协商与会话状态做对，再补齐 2024-11-05 旧传输和 2025-03-26 的批处理差异。

## 当前仓库现状

从现有代码来看，本库已经有一部分“兼容骨架”，但还没有形成完整方案。

### 已有基础

1. `ProtocolVersion` 已经维护了四个历史版本，并区分了 `Current`、`Minimum` 和 `StreamableHttpMinimum`。
2. `InitializeRequestParams` 与 `InitializeResult` 已包含 `protocolVersion` 字段。
3. HTTP 客户端已经会在初始化后缓存服务端返回的协议版本，并把 `Mcp-Protocol-Version` 头带到后续请求里。
4. `LocalHostHttpServerTransportOptions` 已预留旧版 SSE 兼容选项：`IsCompatibleWithSse`、`SseEndPoint`、`SseMessageEndPoint`。
5. `ServerTransportManager` 已对“`initialize` 缺失 id”做了旧客户端兼容处理。

### 当前缺口

1. 服务端 `InitializeAsync` 目前固定返回 `ProtocolVersion.Current`，没有真正执行版本协商。
2. HTTP 服务端只做了“版本低于 2025-03-26 则拒绝”的硬拦截，没有基于会话的版本持续校验，也没有 2024-11-05 兼容路径。
3. 客户端初始化时固定发送 `ProtocolVersion.Current`，没有“支持版本集合”概念，也没有自动回退到旧传输。
4. 当前消息模型默认按最新版本序列化，尚未按协商版本裁剪字段和能力。
5. 2025-03-26 允许 HTTP POST 承载 JSON-RPC batch，而当前 `ServerTransportManager.ReadMessageAsync` 只处理单条消息对象；如果要宣称完整兼容 2025-03-26，这一项必须补齐。
6. 旧版 HTTP+SSE 传输尚未实现，`IsCompatibleWithSse` 目前只是配置入口，不是可工作的能力。
7. TouchSocket 侧的 options 和注释目前明确写着“暂时没考虑兼容旧的 SSE 传输层协议（2024-11-05）”，所以旧协议兼容不能默认视为两套 HTTP 传输同时具备。

## 官方规范差异摘要

### 版本协商共性

四个版本在 lifecycle 上都要求：

1. `initialize` 必须是握手起点。
2. 客户端请求中必须声明自己支持的协议版本。
3. 服务端如果支持该版本，必须回相同版本；否则回自己支持的其他版本。
4. 后续通信必须遵守协商出的版本与能力。

因此，版本协商的核心不是“按字符串比较大小”，而是“从支持矩阵里选一个双方都能执行的 profile”。

### 各版本主要差异

| 版本 | 传输 | 关键差异 | 实现含义 |
| --- | --- | --- | --- |
| 2025-11-25 | Streamable HTTP | 与 2025-06-18 同族，新增 tasks 等能力与更丰富元数据 | 继续作为内部主模型 |
| 2025-06-18 | Streamable HTTP | 已有 `MCP-Protocol-Version` 头与版本协商，能力集低于 2025-11-25 | 需要能力裁剪 |
| 2025-03-26 | Streamable HTTP | 首次引入 Streamable HTTP；HTTP POST 允许 batch；`initialize` 不得放进 batch | 需要单独处理 batch 兼容 |
| 2024-11-05 | HTTP+SSE | 双端点：SSE 建链 + POST 消息；GET `/sse` 必须先发 `endpoint` 事件 | 需要单独传输实现，不能用现有 `/mcp` 逻辑硬凑 |

### 对本库最重要的两个事实

1. 2025-11-25、2025-06-18、2025-03-26 在“内部应用层处理”上可以共用一套主逻辑，但不能假定它们在“消息外形”和“传输细节”上完全相同。
2. 2024-11-05 不是简单的 header 差异，而是独立的 HTTP 交互模型，必须作为另一条传输路径实现。

## 设计原则

### 1. 内部统一，边界投影

内部仍然只维护一套主协议处理器和主消息模型，避免为了兼容多个版本把核心逻辑拆成四份。

### 2. 协商一次，会话绑定

协议版本只在初始化时协商一次，协商结果写入传输层会话。后续所有请求都以会话中的版本为准，不在每次业务处理时重新猜测。

### 3. 兼容规则集中管理

不要把 `if (version == ...)` 分散在 `McpServerRequestHandlers`、HTTP 传输、客户端、序列化器各处。应当引入独立的“协议 profile/兼容层”。

### 4. 两个 HTTP 服务器实现尽量共享同一套规则

`LocalHostHttpServerTransport` 和 `TouchSocketHttpServerTransport` 当前结构基本对称。版本协商、头部校验、错误模型等规则应该抽到共享组件，避免未来两边行为漂移；但 2024-11-05 的旧版 SSE 兼容更适合先在 LocalHost 落地，再决定是否把 TouchSocket 的 public options 一并扩展。

## 总体设计

建议引入如下概念。

### 1. 协议 Profile

新增一个不可变的协议描述对象，例如：

```csharp
internal sealed record McpProtocolProfile(
	 ProtocolVersion Version,
	 McpTransportFamily TransportFamily,
	 bool SupportsStreamableHttp,
	 bool SupportsLegacyHttpSse,
	 bool SupportsHttpBatch,
	 bool SupportsElicitation,
	 bool SupportsTasks,
	 bool SupportsImplementationMetadata,
	 bool RequiresProtocolVersionHeader);
```

建议内置四个 profile：

- `2025-11-25`
- `2025-06-18`
- `2025-03-26`
- `2024-11-05`

其中：

- `2025-11-25`、`2025-06-18`、`2025-03-26` 的 `TransportFamily` 都是 `StreamableHttp`
- `2024-11-05` 的 `TransportFamily` 是 `LegacyHttpSse`

### 2. 版本选择器

新增集中式选择器，例如 `McpProtocolVersionSelector`，负责：

1. 校验客户端请求版本是否是已知版本，或是否允许“未来版本降级”。
2. 从服务器支持列表中选择最终版本。
3. 返回标准错误负载（包含 `requested` 与 `supported`）。

建议策略：

1. 如果客户端请求版本被服务器明确支持，直接选该版本。
2. 如果客户端请求的是“高于当前版本的未知未来版本”，可降级到服务器最新支持版本。
3. 如果客户端请求的是“已知但未支持”的旧版本，且服务器未启用对应兼容实现，则返回初始化错误，不要谎称支持。
4. 如果客户端请求的是无效字符串，则返回 `-32602 Unsupported protocol version`，并带 `supported` 列表。

### 3. 会话状态对象

扩展 `IServerTransportSession` / `ServerTransportSession`，增加至少以下状态：

- `RequestedProtocolVersion`
- `NegotiatedProtocolVersion`
- `NegotiatedProtocolProfile`
- `TransportFamily`
- `IsInitialized`

客户端也要在 `HttpClientTransport` 内部保存：

- `SupportedProtocolVersions`
- `NegotiatedProtocolVersion`
- `NegotiatedTransportFamily`
- `LegacyMessageEndpoint`
- `LastEventId`

### 4. 消息投影层

增加一个协议投影器，例如：

- `McpProtocolNormalizer`：把旧版输入归一成内部主模型
- `McpProtocolProjector`：把内部主模型裁剪成目标版本可接受的外形

这个组件至少需要覆盖：

1. `InitializeResult` 的 `protocolVersion` 写回协商结果。
2. `ServerCapabilities` 的裁剪，例如：低版本不发 `tasks`。
3. `ClientCapabilities` / `Implementation` 的裁剪，例如：较老版本不发新增元数据字段。
4. HTTP 传输行为差异，例如 2024-11-05 的 `endpoint` 事件与 `message` 事件。

## 服务端实现方案

### A. 先抽出共享 HTTP 协议核心

建议不要直接在 `LocalHostHttpServerTransport` 和 `TouchSocketHttpServerTransport` 各自硬改，而是先抽一个共享核心，例如：

- `HttpProtocolRouterCore`
- `LegacyHttpSseSessionCoordinator`
- `StreamableHttpSessionCoordinator`

两套 HTTP 传输只负责：

1. 读取请求
2. 写入响应/SSE
3. 适配底层 HTTP API

所有“路径分发、版本校验、session 建立、legacy endpoint 拼装、错误模型”都走同一个 core。

这样做的原因很直接：当前 LocalHost 与 TouchSocket 两份实现已经基本平行，再把兼容逻辑复制一遍，后续维护成本会明显失控。

### B. 初始化协商流程

服务端初始化流程建议改成：

1. 解析 `InitializeRequestParams.ProtocolVersion`。
2. 调用 `McpProtocolVersionSelector` 选择最终 profile。
3. 把协商结果写入当前 session。
4. 基于 profile 构造 `InitializeResult`。
5. 通过 `McpProtocolProjector` 裁剪响应内容。
6. 返回 JSON-RPC 响应，同时在 HTTP 场景下写入会话头或旧协议 endpoint 信息。

`McpServerRequestHandlers.InitializeAsync` 不建议改成直接知道四个版本的细节，而是：

1. 继续返回“完整内部结果”。
2. 在返回前由兼容层做版本投影。

这样可以保证业务扩展点仍然简洁，兼容逻辑不污染用户自定义处理器。

### C. Streamable HTTP 路径

针对 `/mcp`，建议实现以下规则：

1. `initialize` 之前，允许没有 `Mcp-Protocol-Version` 头。
2. `initialize` 之后：
	- 如果请求头带了版本，则必须与会话协商结果一致。
	- 如果没带头，则优先使用会话中的协商版本；对于无法识别版本的无状态场景，再按规范 fallback 到 `2025-03-26`。
3. 如果请求头版本无效或服务端不支持，返回 `400 Bad Request`。
4. `GET /mcp` 与 `POST /mcp` 使用同一套会话版本信息。
5. `DELETE /mcp` 也要校验会话与版本，而不是只看 `Mcp-Session-Id`。

### D. 2025-03-26 的 batch 兼容

这是一个容易漏掉但不能忽略的点。

如果要对外宣称完整支持 `2025-03-26`，服务端必须补齐：

1. HTTP POST body 可解析 JSON-RPC batch。
2. batch 中只要包含 request，就要走 request 响应路径。
3. `initialize` 不能出现在 batch 中，出现即返回协议错误。
4. SSE 返回时，要支持“一次请求对应多个响应”的 2025-03-26 语义。

如果短期不打算做 batch，那么文档中不能写“已支持 2025-03-26”，只能写“支持其单消息子集”。

### E. 2024-11-05 旧版 HTTP+SSE 路径

这个版本建议作为独立 transport family 实现，而不是塞进 `/mcp` 的条件分支里。

从当前仓库现状看，这部分应当分两步做：

1. 先在 `LocalHostHttpServerTransport` 完整支持，因为它已经有 `IsCompatibleWithSse` 配置入口。
2. 再决定是否把相同能力扩展到 TouchSocket；如果不扩展，就必须在文档中明确“TouchSocket 仅支持 Streamable HTTP”。

服务端规则应当是：

1. `GET /mcp/sse`
	- 建立 SSE 连接
	- 立即发送 `event: endpoint`
	- `data` 为带 `sessionId` 的消息提交地址
2. `POST /mcp/messages?sessionId=...`
	- 接收客户端后续所有消息，包括 `initialize`
	- 返回普通 HTTP 状态
3. 服务端对客户端消息通过 SSE `message` 事件发送
4. 旧协议路径不要求 `Mcp-Protocol-Version` 头

仓库里已有 `IsCompatibleWithSse` 选项，因此服务端 API 设计上建议保持以下形式：

```csharp
new LocalHostHttpServerTransportOptions
{
	 Port = 3001,
	 EndPoint = "/mcp",
	 IsCompatibleWithSse = true,
}
```

但实现上要真正让这个选项生效。

### F. 能力与字段裁剪

建议按“目标版本 profile”裁剪以下内容：

1. `InitializeResult.Capabilities`
	- 低版本不发 `tasks`
	- 低版本不发高版本才出现的子能力
2. `InitializeResult.ServerInfo`
	- 对较老版本只保留 `name`、`version`
	- 较新版本再补 `title`、`description`、`icons`、`websiteUrl`
3. 运行期服务端主动消息
	- 只发送目标版本定义过的方法与字段

原则上不要把“旧客户端会忽略未知字段”当作正式兼容策略。那只能算“碰巧能跑”，不算协议级兼容。

## 客户端实现方案

### A. 客户端配置面

建议扩展 `HttpClientTransportOptions`，至少增加：

- `SupportedProtocolVersions`
- `PreferredProtocolVersion`
- `EnableLegacyHttpSseFallback`
- `AllowFutureVersionDowngrade`

`McpClientBuilder.WithHttp(...)` 默认值可以是：

1. 支持 `2025-11-25`、`2025-06-18`、`2025-03-26`
2. 可选启用 `2024-11-05`
3. 默认首选最新版本

### B. 初始化策略

客户端初始化建议遵循：

1. 首先按首选版本向 `/mcp` 发送 Streamable HTTP `initialize`。
2. 若成功，则检查服务端返回的 `protocolVersion` 是否在本地支持列表内。
3. 若服务端返回本地不支持的版本，立即断开。
4. 若 POST 初始化失败，且状态码满足规范中的回退条件（`400` / `404` / `405`），再尝试旧版 HTTP+SSE 探测。

### C. 旧版 HTTP+SSE 自动探测

客户端对服务器 URL 的兼容逻辑建议按规范实现：

1. 先尝试对用户给出的 URL 执行 Streamable HTTP 初始化 POST。
2. 如果返回 `400`、`404` 或 `405`，则尝试 GET 建立 SSE。
3. 如果首个事件是 `endpoint`，认定为 2024-11-05 服务器。
4. 之后所有客户端消息都发往 `endpoint` 事件给出的地址。

这样客户端才能真正做到“用户给一个 URL，库自动识别新旧协议”。

### D. 后续请求行为

1. Streamable HTTP 模式下：初始化后所有 GET/POST/DELETE 均携带协商出的 `Mcp-Protocol-Version`。
2. Legacy HTTP+SSE 模式下：不要强行加新协议头，按旧协议 endpoint 与 sessionId 工作。
3. 如果收到 `404 + Mcp-Session-Id`，按规范重新初始化新会话。
4. 如果将来实现 resumable stream，则 `Last-Event-ID` 也需要绑定在协商后的 transport family 上。

## 推荐代码结构

建议按下面的方向拆分代码。

### 新增或重构的核心文件

| 位置 | 建议改动 |
| --- | --- |
| `src/DotNetCampus.ModelContextProtocol/Protocol/ProtocolVersion.cs` | 增加已知版本判断、版本选择辅助方法，避免外部只靠字符串比较 |
| `src/DotNetCampus.ModelContextProtocol/Protocol/Compatibility/` | 新增 profile、selector、projector、normalizer 等兼容层核心 |
| `src/DotNetCampus.ModelContextProtocol/Transports/IServerTransportSession.cs` | 增加协商版本、profile、transport family 等会话状态 |
| `src/DotNetCampus.ModelContextProtocol/Transports/ServerTransportSession.cs` | 落地会话协商状态 |
| `src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs` | 初始化时使用版本选择器，而不是固定返回 `Current` |
| `src/DotNetCampus.ModelContextProtocol/Transports/Http/LocalHostHttpServerTransport.cs` | 接入共享 HTTP 协议核心，支持 legacy 路由 |
| `src/DotNetCampus.ModelContextProtocol.TouchSocket.Http/Transports/TouchSocket/TouchSocketHttpServerTransport.cs` | 共享版本协商与 Streamable HTTP 规则；若要支持 2024-11-05，还需同步扩展 options |
| `src/DotNetCampus.ModelContextProtocol.TouchSocket.Http/Transports/TouchSocket/TouchSocketHttpServerTransportOptions.cs` | 若决定支持 2024-11-05，需要补齐 legacy SSE 相关配置面 |
| `src/DotNetCampus.ModelContextProtocol/Transports/Http/HttpClientTransport.cs` | 引入支持版本集合、旧版 fallback 和双 transport family 处理 |
| `src/DotNetCampus.ModelContextProtocol/Transports/Http/HttpClientTransportOptions.cs` | 暴露客户端兼容配置 |
| `src/DotNetCampus.ModelContextProtocol/Clients/McpClientBuilder.cs` | 提供更清晰的兼容配置入口 |

### 尽量不要改动太深的部分

以下部分尽量保持稳定，只在边缘接入兼容层：

- `McpProtocolBridge`
- 工具、资源的业务处理流程
- Source Generator 生成出来的工具/资源发现机制

原因是版本兼容主要发生在传输边界与初始化阶段，不应该把核心业务分发也改成版本驱动。

## 分阶段实施计划

### 第一阶段：把协商状态做对

目标：先让“版本协商”真正成立。

1. 引入 `McpProtocolProfile` 与 `McpProtocolVersionSelector`
2. 扩展 session 状态
3. 修改 `InitializeAsync`，返回协商后的版本
4. 客户端记录支持版本集合与协商结果
5. 后续请求头按会话版本校验

交付标准：

- 服务端不再固定回 `2025-11-25`
- 初始化失败时返回标准错误模型
- 单纯的 Streamable HTTP 版本协商可用

### 第二阶段：补齐 Streamable HTTP 的版本差异

目标：把 2025-03-26、2025-06-18、2025-11-25 的差异从“字符串兼容”升级为“行为兼容”。

1. 按 version profile 裁剪 `InitializeResult`
2. 运行期消息按协商版本裁剪
3. 补齐 2025-03-26 的 batch 支持，或明确标记为部分兼容

交付标准：

- 不同版本客户端看到的能力集合不同且合理
- 2025-03-26 的传输差异被准确处理

### 第三阶段：实现 2024-11-05 旧版 HTTP+SSE

目标：真正支持 legacy transport。

1. 实现 `/mcp/sse`
2. 实现 `/mcp/messages?sessionId=...`
3. 客户端实现 `endpoint` 事件探测与切换
4. 打通初始化、工具调用、服务端主动消息全链路

交付标准：

- 新客户端可以自动连接旧服务器
- 新服务器可选兼容旧客户端

### 第四阶段：收敛 API 与文档

目标：把兼容能力变成稳定、可理解的公共 API。

1. 收敛 builder/options 暴露的兼容配置
2. 更新 README 与 `docs/knowledge` 说明
3. 明确声明“哪些版本完全兼容，哪些是部分兼容”

## 测试计划

建议把测试集中放在 `tests/DotNetCampus.ModelContextProtocol.Tests` 下，并优先扩展现有 HTTP/Client/Compliance 测试。

### 1. 版本协商测试

建议新增或扩展：

- `Transports/HttpTransportTests.cs`
- `Clients/McpClientTests.cs`
- `Compliance/OfficialServerTests.cs`

关键用例：

1. 客户端请求 `2025-11-25`，服务端支持该版本，返回相同版本。
2. 客户端请求 `2025-11-25`，服务端只支持 `2025-06-18`，返回 `2025-06-18`。
3. 客户端请求无效版本，服务端返回 `-32602` 与 `supported` 列表。
4. 初始化后发送与协商版本不一致的 `Mcp-Protocol-Version` 头，服务端返回 `400`。

### 2. Streamable HTTP 测试

1. `POST /mcp` 初始化返回协商后的 `protocolVersion` 与 `Mcp-Session-Id`
2. `GET /mcp` 读取的会话版本与初始化一致
3. `DELETE /mcp` 在不同版本下都能正确终止会话
4. 2025-03-26 batch 的正反向用例

### 3. Legacy HTTP+SSE 测试

1. `GET /mcp/sse` 首个事件是 `endpoint`
2. `POST /mcp/messages?sessionId=...` 能完成 `initialize`
3. 服务端主动消息通过 SSE `message` 事件送达
4. 旧协议路径不需要新协议头

### 4. 投影测试

1. 低版本初始化响应不包含高版本 capability
2. `Implementation` 在不同版本下输出字段不同
3. 服务端主动请求在低版本下不会发出未定义字段

## 风险与取舍

### 风险 1：只做协商，不做投影

这样最容易“看起来支持多版本，实际上只支持最新消息模型”。短期可跑，长期会在严格客户端上暴露兼容问题。

### 风险 2：两套 HTTP 实现分别修改

会导致 LocalHost 与 TouchSocket 在路径、头校验、错误码、legacy 行为上逐步漂移。这个风险应该通过共享协议核心消除。

### 风险 3：过早把核心业务处理也版本化

会让工具、资源、请求分发全线复杂化。正确做法是把版本兼容限制在“传输边界 + 初始化 + 消息投影”三层。

### 风险 4：对 2025-03-26 的 batch 支持半做半不做

如果不支持，就必须明确写“部分兼容”；否则会造成对外声明与实际行为不一致。

## 推荐落地顺序

建议按以下顺序推进，而不是一次性铺开：

1. 先把协商状态、版本选择器和初始化错误模型做完。
2. 再把 Streamable HTTP 的后续请求校验和消息投影做完。
3. 然后实现 2024-11-05 的服务端旧路径。
4. 最后给客户端补自动探测和 legacy fallback。

这样可以确保每一步都有明确验收点，不会把“版本协商”和“旧传输兼容”纠缠在一起。

## 最终建议

如果这项工作要进入正式开发，我建议把目标定为：

1. 内部维持 `2025-11-25` 主模型不变。
2. 外围新增一层显式的协议兼容层。
3. 对 Streamable HTTP 与 Legacy HTTP+SSE 采用双 transport family 设计。
4. 以测试矩阵驱动声明式支持，而不是仅凭文档描述“理论兼容”。

用一句话总结：

> 本库应采用“最新内核 + 版本 profile + 会话绑定协商 + 边界投影 + 双 HTTP 传输族”的方案实现多版本兼容，而不是在现有传输实现上继续追加零散条件分支。
