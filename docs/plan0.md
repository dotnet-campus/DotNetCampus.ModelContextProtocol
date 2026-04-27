# 2024-11-05 服务端兼容计划

本文只规划一件事：让本库的两个 HTTP 服务端传输层兼容 MCP 2024-11-05 的 HTTP with SSE。

本期目标限定在：

1. `LocalHostHttpServerTransport` 支持 2024-11-05。
2. `TouchSocketHttpServerTransport` 支持 2024-11-05。
3. 兼容代码尽量独立，避免破坏现有新协议实现。
4. 新协议热路径的性能与行为保持稳定。

不在本文范围内的内容不再展开，包括客户端传输层、其他协议版本的统一兼容架构，以及更大范围的版本协商设计。那部分长期方案继续放在 `未来plan.md`。

## 协议边界

2024-11-05 的 HTTP with SSE 有几条直接影响实现的约束：

1. 服务端需要两个端点：一个 SSE 端点，一个普通 HTTP POST 端点。
2. 客户端连上 SSE 端点后，服务端必须先发送 `endpoint` 事件。
3. `endpoint` 事件里要告诉客户端后续 POST 的目标地址。
4. 服务端发往客户端的消息通过 SSE `message` 事件发送，事件数据是 JSON-RPC 消息。
5. 服务端仍然需要做 `Origin` 校验。
6. `initialize` 返回的 `protocolVersion` 必须是 `2024-11-05`。

除此之外，本文采取“最小兼容”原则：凡是规范没有明确要求必须裁剪的内容，不预先做过度保护；如果后续联调用例证明旧客户端无法接受，再追加有针对性的适配。

## 设计原则

### 1. 旧协议逻辑独立放置

旧协议兼容尽量拆到单独文件，而不是直接揉进现有核心流程。优先考虑在基础库里新增一组共享类型，由 `LocalHostHttpServerTransport` 和 `TouchSocketHttpServerTransport` 只做轻量接入。

如果某段逻辑必须留在原类中，也应拆成单独的 `#region Legacy SSE (2024-11-05)`，避免和现有 Streamable HTTP 路径交错。

### 2. 新协议热路径不受影响

`/mcp` 对应的现有 Streamable HTTP 流程继续保持原样。旧协议逻辑只在命中 `/sse` 与 `/messages` 这两个 legacy 端点时才进入。

目标是让：

1. 新协议请求不创建 legacy 对象。
2. 新协议请求不进入 legacy 判断链的深层逻辑。
3. 开启旧协议兼容后，新协议的可观测行为不变。

### 3. 两个服务端共用一套旧协议核心

`LocalHost` 和 `TouchSocket` 的底层 HTTP API 不同，但 2024-11-05 的协议规则是相同的。旧协议的 session 管理、SSE 事件格式、消息桥接、初始化适配，应该尽量共用一套实现。

这样可以把差异尽量收敛到“如何读请求、如何写响应、如何保持 SSE 连接”这层适配，而不是把同一份兼容逻辑复制两遍。

### 4. 先满足规范硬约束，再看互操作性补丁

本期必须先满足的是旧协议传输形态和 `protocolVersion` 返回值。至于 `serverInfo`、`capabilities` 是否需要额外裁剪，先不要在计划里预设太多规则。

建议的顺序是：

1. 先让旧客户端按 2024-11-05 的方式成功连上并完成 `initialize`。
2. 默认尽量复用当前消息模型。
3. 如果旧客户端对新增字段、能力或消息方法存在兼容问题，再做最小范围的定点适配。

## 建议结构

建议在基础库中增加一组共享的 legacy 组件，例如：

1. `LegacySseSession`
2. `LegacySseEventWriter`
3. `LegacySseRequestRouter`
4. `LegacyInitializeResponseAdapter`
5. `LegacySseEndpointInfo`

可以放在如下位置：

1. `src/DotNetCampus.ModelContextProtocol/Transports/Http/Legacy/`
2. 或 `src/DotNetCampus.ModelContextProtocol/Transports/LegacyHttpSse/`

两个服务端传输层只保留薄适配：

1. 路由识别 `/sse` 与 `/messages`
2. 创建/查找 legacy session
3. 把请求对象转给共享核心
4. 把共享核心输出写回各自的 HTTP/SSE API

如果后续证明这套共享抽象不够顺手，再退一步，把每个传输层中的旧协议部分拆成单独区域，但仍然保持独立方法和独立文件，不直接污染现有 `/mcp` 主流程。

## 兼容入口设计

两个服务端都需要支持如下 legacy 端点：

1. `GET {EndPoint}/sse`
2. `POST {EndPoint}/messages`

其中：

1. `GET /sse` 负责建立 SSE 连接并发送 `endpoint` 事件。
2. `POST /messages` 负责接收客户端后续发来的 JSON-RPC 消息。

旧协议的连接时序建议统一为：

1. 客户端请求 `GET /sse`。
2. 服务端创建 legacy session。
3. 服务端返回 `text/event-stream`。
4. 服务端立即发送 `event: endpoint`。
5. `data` 中带上当前 session 的消息提交地址。
6. 客户端之后持续向 `/messages` 发 POST。
7. 服务端产生的 JSON-RPC 响应和服务端主动消息，都通过 SSE `message` 事件返回。

这里要注意，2024-11-05 不是当前 `/mcp` 的变体，而是另一套传输形态。因此不要把现有 `application/json` 或 `text/event-stream` 的 `/mcp` 响应策略直接套到 `/messages` 上。

## Session 设计

建议为旧协议使用独立 session 类型，不直接复用现有 `HttpServerTransportSession`。

这个 session 至少需要承担：

1. 维护 `sessionId`
2. 保存 SSE 输出目标
3. 发送 `endpoint` 事件
4. 发送 `message` 事件
5. 感知连接断开并做清理
6. 把服务端回包与主动消息统一投递到 SSE 通道

这样做的好处是：

1. 旧协议的事件格式不会污染现有 Streamable HTTP session。
2. `LocalHost` 和 `TouchSocket` 都能围绕同一个 legacy session 抽象做适配。
3. 后续若要补更多 2024-11-05 细节，也不会牵动 `/mcp` 主流程。

## initialize 兼容策略

本期对 `initialize` 的处理采用“硬要求最少化、适配后置化”的策略。

必须落实的内容：

1. legacy 路径收到 `initialize` 后，返回结果中的 `protocolVersion` 必须是 `2024-11-05`。
2. legacy 路径下的请求与响应都走旧协议通道，不混用当前 `/mcp` 的头部和会话规则。

初版不必预先做大量字段裁剪。建议先按以下方式处理：

1. 默认复用当前 `InitializeResult` 的主体生成逻辑。
2. 在 legacy 路径上仅强制改写 `protocolVersion`。
3. 其余字段保持现状，除非：
   - 规范明确要求不能这样做
   - 旧客户端联调时确实失败

如果后续验证发现某些旧客户端无法接受新增字段，再新增一个轻量的 `LegacyInitializeResponseAdapter`，专门做定点裁剪，而不是一开始就铺开一整套通用投影框架。

## 开关与默认值

当前 `LocalHostHttpServerTransportOptions.IsCompatibleWithSse` 默认为 `false`。这一点不必在计划阶段先写死最终结论，但建议按下面的顺序推进：

1. 先让两套服务端都具备旧协议能力。
2. 让旧协议代码结构上与新协议热路径隔离，做到不开启时几乎无额外代价。
3. 在兼容模式关闭时，如果命中了明显的旧协议访问特征，就返回更清晰的错误信息，提示开发者开启兼容模式。
4. 等实现完成并通过回归与性能验证后，再决定默认值是否要调整为 `true`。

TouchSocket 侧也建议补一个对称的开关配置，而不是把兼容逻辑写成始终开启但不可控的状态。

## 性能要求

本期兼容旧协议时，性能目标应明确为：

1. 使用新协议连接时，不引入可观测的性能退化。
2. 使用旧协议连接时，可以接受适度损耗，但不要出现明显的额外对象堆积和不必要复制。
3. 两套传输层都尽量复用现有 JSON-RPC 读写与应用层桥接能力。

实现上建议注意：

1. legacy 端点判断尽量前置且浅层。
2. 只有命中 legacy 路径时才创建 legacy session 与 SSE writer。
3. 不要让新协议请求进入 legacy 的复杂分支。

## 分步实施

### 第一步：补齐共享 legacy 核心

1. 新增 legacy session、event writer、endpoint builder、请求分发等共享类型。
2. 明确 LocalHost 与 TouchSocket 各自需要实现的薄适配接口。

完成标志：

1. 共享核心不依赖具体 HTTP 实现。
2. 两个传输层都能接入这套核心。

### 第二步：接入 LocalHost

1. 为 `LocalHostHttpServerTransport` 增加 `/sse` 与 `/messages` 路由。
2. 接入 legacy session 生命周期管理。
3. 让旧协议响应通过 SSE `message` 事件发送。

完成标志：

1. `LocalHost` 能完成 `GET /sse` 建链。
2. `endpoint` 事件格式正确。
3. `initialize` 与至少一条普通请求能走通。

### 第三步：接入 TouchSocket

1. 让 `TouchSocketHttpServerTransport` 对称支持 `/sse` 与 `/messages`。
2. 接入同一套 legacy 核心。
3. 补齐 TouchSocket 对应的配置开关和错误提示。

完成标志：

1. `TouchSocket` 的旧协议行为与 `LocalHost` 对齐。
2. 两个服务端对旧协议返回一致的传输语义。

### 第四步：联调与定点适配

1. 用旧客户端验证 `initialize`、普通请求、服务端回包。
2. 若发现旧客户端对新增字段或消息不兼容，再追加定点裁剪。
3. 评估兼容开关默认值与错误提示策略。

完成标志：

1. 旧客户端能连通两个服务端。
2. 当前新协议路径回归通过。
3. 若有必要的裁剪，范围被限制在 legacy 适配层中。

## 建议改动位置

建议优先落在以下文件或相邻新文件中：

1. `src/DotNetCampus.ModelContextProtocol/Transports/Http/Legacy/**`
2. `src/DotNetCampus.ModelContextProtocol/Transports/Http/LocalHostHttpServerTransport.cs`
3. `src/DotNetCampus.ModelContextProtocol/Transports/Http/LocalHostHttpServerTransportOptions.cs`
4. `src/DotNetCampus.ModelContextProtocol.TouchSocket.Http/Transports/TouchSocket/TouchSocketHttpServerTransport.cs`
5. `src/DotNetCampus.ModelContextProtocol.TouchSocket.Http/Transports/TouchSocket/TouchSocketHttpServerTransportOptions.cs`
6. `src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs`

其中，`McpServerRequestHandlers` 只在 legacy `initialize` 的必要适配点上做改动，不建议把大段旧协议逻辑挪进请求处理主流程。

## 测试计划

测试应直接围绕两个服务端传输层展开，避免再扩散到整套多版本兼容话题。

优先补在现有 HTTP 测试体系中，并复用已经存在的 `HttpTransportType.LocalHost` / `HttpTransportType.TouchSocket` 双通道测试模式。

至少覆盖以下用例：

1. `GET /sse` 成功建立连接，并首先收到 `endpoint` 事件。
2. `POST /messages` 可以完成 `initialize`。
3. `initialize` 返回的 `protocolVersion` 为 `2024-11-05`。
4. 普通工具调用的响应能够通过 SSE `message` 事件送达。
5. 兼容开关关闭时，访问旧协议端点能得到清晰错误。
6. 新协议 `/mcp` 的现有行为在两种服务端上都不回退。
7. 如果后续追加字段裁剪，对应增加回归测试，防止适配范围继续膨胀。

## 验收标准

本期完成后，应达到：

1. 旧客户端可以连接 `LocalHostHttpServerTransport`。
2. 旧客户端可以连接 `TouchSocketHttpServerTransport`。
3. 两个服务端都能通过 `/sse` + `/messages` 完成 `initialize` 和至少一条普通请求。
4. 新协议 `/mcp` 现有能力和性能不出现明显回退。
5. 旧协议兼容代码主要集中在独立文件或清晰区域内，没有大面积污染现有核心实现。

## 一句话结论

当前阶段最合适的做法，是为 `LocalHostHttpServerTransport` 和 `TouchSocketHttpServerTransport` 增加一套共享的 2024-11-05 legacy 适配层：旧协议逻辑独立放置，两个传输层只做薄接入，`initialize` 先满足硬约束，其余兼容行为按规范和联调结果做最小增量适配。