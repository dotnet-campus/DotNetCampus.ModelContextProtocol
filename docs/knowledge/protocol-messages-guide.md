# MCP 协议消息类型注释规范

> 本文档规定了 DotNetCampus.ModelContextProtocol 项目中所有 Protocol 消息类型的注释标准。

## 📌 当前使用的 MCP 协议版本

**最新协议版本**: `2025-06-18`

**官方 Schema 文件**: [https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/schema/2025-06-18/schema.ts](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/schema/2025-06-18/schema.ts)

## 📋 注释格式规范

### 基本格式要求

所有 Protocol 文件夹下的消息类型和属性都必须添加**中英双语注释**，格式如下：

```csharp
/// <summary>
/// 中文描述（使用中文标点符号：，（）。）<br/>
/// English description (from official MCP schema)
/// </summary>
public record SomeMessage
{
    /// <summary>
    /// 属性的中文描述<br/>
    /// Property description from official schema
    /// </summary>
    [JsonPropertyName("propertyName")]
    public string PropertyName { get; init; }
}
```

### 详细格式规则

#### 1. 中文注释规则

- ✅ **中文在前**：中文描述必须在英文之前
- ✅ **中文标点**：全部使用中文标点符号
  - 逗号：`，`（而非 `,`）
  - 句号：`。`（而非 `.`）
  - 括号：`（）`（而非 `()`）
  - 冒号：`：`（而非 `:`）
- ✅ **换行标记**：中文末尾使用 `<br/>`
- ✅ **标点一致性**：
  - 如果英文有末尾句号，中文也要有
  - 如果英文没有末尾句号，中文也不要有

#### 2. 英文注释规则

- ✅ **新行开始**：英文注释在 `<br/>` 之后的新行
- ✅ **官方原文**：必须使用 MCP 官方 schema 文件中的原始描述
- ✅ **完整准确**：不得修改或简化官方描述
- ✅ **适当换行**：长注释应适当换行以提高可读性（每行不超过 100-120 字符）

#### 3. 多行注释示例

```csharp
/// <summary>
/// 工具调用是否以错误结束。<br/>
/// 如果未设置，则假定为 false（调用成功）。<br/>
/// 源自工具的任何错误都应该在结果对象内报告，并将 isError 设置为 true，
/// 而不是作为 MCP 协议级别的错误响应。<br/>
/// 否则，LLM 将无法看到发生了错误并进行自我纠正。<br/>
/// Whether the tool call ended in an error.<br/>
/// If not set, this is assumed to be false (the call was successful).<br/>
/// Any errors that originate from the tool SHOULD be reported inside the result object,
/// with isError set to true, _not_ as an MCP protocol-level error response.
/// Otherwise, the LLM would not be able to see that an error occurred and self-correct.
/// </summary>
```

## 🗂️ 文件结构与组织

### Protocol 文件夹结构

```
src/DotNetCampus.ModelContextProtocol/Protocol/
├── Messages/                      # MCP 消息类型
│   ├── JsonRpc/                  # JSON-RPC 基础消息
│   │   ├── JsonRpcMessage.cs
│   │   ├── JsonRpcRequest.cs
│   │   ├── JsonRpcResponse.cs
│   │   ├── JsonRpcNotification.cs
│   │   ├── JsonRpcError.cs
│   │   └── JsonRpcErrorCode.cs
│   ├── ProtocolBase.cs           # MCP 基类（RequestParams, Result, PaginatedRequestParams, PaginatedResult）
│   ├── InitializeRequestParams.cs
│   ├── InitializeResult.cs
│   ├── ClientCapabilities.cs
│   ├── ServerCapabilities.cs
│   ├── Implementation.cs
│   ├── ServerInfo.cs
│   ├── Icon.cs
│   ├── Annotations.cs
│   ├── Tool.cs
│   ├── ToolAnnotations.cs
│   ├── CallToolRequestParams.cs
│   ├── CallToolResult.cs
│   ├── ListToolsRequestParams.cs
│   ├── ListToolsResult.cs
│   ├── PingRequestParams.cs
│   └── ContentBlock.cs           # 内容块及相关类型（多个类）
├── ITransport.cs                  # 传输层接口
└── RequestMethods.cs              # 请求方法常量
```

### 命名约定

#### 1. 请求参数类

- 命名格式：`{功能}RequestParams`
- 继承：`RequestParams` 或 `PaginatedRequestParams`
- 示例：
  - `InitializeRequestParams`
  - `CallToolRequestParams`
  - `ListToolsRequestParams : PaginatedRequestParams`

#### 2. 响应结果类

- 命名格式：`{功能}Result`
- 继承：`Result` 或 `PaginatedResult`
- 示例：
  - `InitializeResult`
  - `CallToolResult`
  - `ListToolsResult : PaginatedResult`

#### 3. 能力类

- 命名格式：`{角色}Capabilities` 或 `{功能}Capability`
- 示例：
  - `ClientCapabilities`
  - `ServerCapabilities`
  - `ToolsCapability`
  - `ResourcesCapability`

## 📚 消息类型注释清单

### JSON-RPC 基础类型

这些类型参考 [JSON-RPC 2.0 规范](https://www.jsonrpc.org/specification#error_object)。

| 类型                  | 文件                     | 说明                  |
| --------------------- | ------------------------ | --------------------- |
| `JsonRpcMessage`      | `JsonRpcMessage.cs`      | JSON-RPC 2.0 消息基类 |
| `JsonRpcRequest`      | `JsonRpcRequest.cs`      | 期望响应的请求        |
| `JsonRpcResponse`     | `JsonRpcResponse.cs`     | 成功响应              |
| `JsonRpcNotification` | `JsonRpcNotification.cs` | 不期望响应的通知      |
| `JsonRpcError`        | `JsonRpcError.cs`        | 错误信息              |
| `JsonRpcErrorCode`    | `JsonRpcErrorCode.cs`    | 标准错误码枚举        |

### MCP 核心类型

以下类型参考 [MCP Schema 2025-06-18](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/schema/2025-06-18/schema.ts)。

#### 基础消息类型

| 类型                     | Schema 对应        | 说明                            |
| ------------------------ | ------------------ | ------------------------------- |
| `RequestParams`          | `Request.params`   | 请求参数基类，包含 `_meta` 字段 |
| `Result`                 | `Result`           | 响应结果基类，包含 `_meta` 字段 |
| `PaginatedRequestParams` | `PaginatedRequest` | 分页请求参数，包含 `cursor`     |
| `PaginatedResult`        | `PaginatedResult`  | 分页响应，包含 `nextCursor`     |

#### 初始化相关

| 类型                      | Schema 对应                | 说明                               |
| ------------------------- | -------------------------- | ---------------------------------- |
| `InitializeRequestParams` | `InitializeRequest.params` | 初始化请求参数                     |
| `InitializeResult`        | `InitializeResult`         | 初始化响应结果                     |
| `ClientCapabilities`      | `ClientCapabilities`       | 客户端能力声明                     |
| `ServerCapabilities`      | `ServerCapabilities`       | 服务端能力声明                     |
| `Implementation`          | `Implementation`           | MCP 实现信息（名称、版本、标题等） |
| `ServerInfo`              | 服务端信息简化版           | 服务器名称和版本                   |

#### 工具相关

| 类型                     | Schema 对应              | 说明                 |
| ------------------------ | ------------------------ | -------------------- |
| `Tool`                   | `Tool`                   | 工具定义             |
| `ToolAnnotations`        | `ToolAnnotations`        | 工具注解（提示信息） |
| `CallToolRequestParams`  | `CallToolRequest.params` | 工具调用请求         |
| `CallToolResult`         | `CallToolResult`         | 工具调用结果         |
| `ListToolsRequestParams` | `ListToolsRequest`       | 列出工具请求         |
| `ListToolsResult`        | `ListToolsResult`        | 列出工具响应         |

#### 内容块相关

| 类型                           | Schema 对应            | 说明                 |
| ------------------------------ | ---------------------- | -------------------- |
| `ContentBlock`                 | `ContentBlock`         | 内容块基类（多态）   |
| `TextContentBlock`             | `TextContent`          | 文本内容             |
| `ImageContentBlock`            | `ImageContent`         | 图像内容             |
| `AudioContentBlock`            | `AudioContent`         | 音频内容             |
| `ResourceLinkContentBlock`     | `ResourceLink`         | 资源链接             |
| `EmbeddedResourceContentBlock` | `EmbeddedResource`     | 嵌入资源             |
| `ResourceContents`             | `ResourceContents`     | 资源内容基类（多态） |
| `TextResourceContents`         | `TextResourceContents` | 文本资源             |
| `BlobResourceContents`         | `BlobResourceContents` | 二进制资源（Base64） |

#### 通用类型

| 类型          | Schema 对应   | 说明                                           |
| ------------- | ------------- | ---------------------------------------------- |
| `Annotations` | `Annotations` | 客户端注解（audience, priority, lastModified） |
| `Icon`        | `Icon`        | 图标信息                                       |

#### 常量和接口

| 类型             | 说明                 |
| ---------------- | -------------------- |
| `RequestMethods` | 所有请求方法名称常量 |
| `ITransport`     | 传输层接口           |

## 🔄 协议版本更新流程

### 当 MCP 发布新协议版本时

1. **检查官方 Schema**
   - 访问 [MCP GitHub Schema 目录](https://github.com/modelcontextprotocol/modelcontextprotocol/tree/main/schema)
   - 找到最新版本的 `schema.ts` 文件（格式：`YYYY-MM-DD/schema.ts`）
   - 记录新版本号和 Schema 文件 URL

2. **对比变更**
   - 使用 diff 工具对比新旧 schema.ts
   - 识别新增、修改、废弃的消息类型和属性
   - 特别关注：
     - 新增的请求/响应类型
     - 修改的字段描述
     - 新增的枚举值或常量
     - 废弃的功能（查看 `@deprecated` 标记）

3. **更新代码**
   
   #### 3.1 新增消息类型
   - 在 `Protocol/Messages/` 下创建新的 `.cs` 文件
   - 遵循命名约定（`{功能}RequestParams`, `{功能}Result`）
   - 添加符合格式规范的双语注释
   - 英文注释**必须**使用官方 schema 原文

   #### 3.2 修改现有类型
   - 定位到对应的 `.cs` 文件
   - 更新属性定义
   - 更新注释（英文部分使用新 schema 原文）
   - 如果是**破坏性变更**，考虑保留旧版本并标记为 `[Obsolete]`

   #### 3.3 更新常量
   - 检查 `RequestMethods.cs`，添加新的请求方法常量
   - 检查 `JsonRpcErrorCode.cs`，添加新的错误码

4. **更新文档**
   - 更新本文档（`protocol-messages-guide.md`）
   - 在顶部更新**当前使用的 MCP 协议版本**
   - 更新消息类型清单
   - 记录重要的变更说明

5. **验证**
   - 运行 `dotnet build` 确保无编译错误
   - 检查所有注释格式是否符合规范
   - 验证英文注释与官方 schema 一致

## ⚠️ 特殊注意事项

### 1. `_meta` 字段的注释

所有包含 `_meta` 字段的类型，其注释应统一引用官方文档链接：

```csharp
/// <summary>
/// 元数据字段<br/>
/// See <a href="https://modelcontextprotocol.io/specification/2025-06-18/basic/index#meta">
/// General fields: _meta</a> for notes on _meta usage.
/// </summary>
[JsonPropertyName("_meta")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public JsonObject? Meta { get; init; }
```

### 2. BaseMetadata 相关字段

根据 MCP Schema，`name` 和 `title` 有特定的语义：

- **`name`**: 用于编程或逻辑使用，但在过去的规范中或作为后备用于显示名称（如果 title 不存在）
- **`title`**: 用于 UI 和最终用户上下文 — 优化为可读并易于理解

对于 `Tool` 类型，显示名称的优先级是：`title` → `annotations.title` → `name`

### 3. 提示（Hints）性质的字段

某些字段（如 `ToolAnnotations` 中的所有属性）是**提示性质**的，注释中应强调：

```csharp
/// <summary>
/// 向客户端描述工具的额外属性。<br/>
/// 注意：ToolAnnotations 中的所有属性都是提示。<br/>
/// 它们不保证提供对工具行为的忠实描述（包括像 title 这样的描述性属性）。<br/>
/// 客户端不应基于从不可信服务器收到的 ToolAnnotations 来做出工具使用决策。<br/>
/// Additional properties describing a Tool to clients.<br/>
/// NOTE: all properties in ToolAnnotations are **hints**.<br/>
/// They are not guaranteed to provide a faithful description of tool behavior
/// (including descriptive properties like title).<br/>
/// Clients should never make tool use decisions based on ToolAnnotations
/// received from untrusted servers.
/// </summary>
```

### 4. 多态类型（Polymorphic Types）

对于使用 `[JsonPolymorphic]` 的类型：

- 基类应说明其多态性质
- 使用 `[JsonDerivedType]` 标记所有派生类型
- 每个派生类型都应有独立的注释

示例：

```csharp
/// <summary>
/// 内容块<br/>
/// Content block that can be part of a message or result
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContentBlock), typeDiscriminator: "text")]
[JsonDerivedType(typeof(ImageContentBlock), typeDiscriminator: "image")]
// ... 其他派生类型
public abstract record ContentBlock
```

### 5. 分页支持

分页相关的类型必须正确继承和实现：

- **请求**：继承 `PaginatedRequestParams`，包含 `cursor` 属性
- **响应**：继承 `PaginatedResult`，包含 `nextCursor` 属性

### 6. 可空性和 JsonIgnore

遵循以下原则：

- 可选字段使用 nullable 类型（`string?`, `int?`）
- 可选字段添加 `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`
- 必需字段使用 `required` 修饰符

## 📖 参考资料

### 官方文档

- **MCP 官方网站**: [https://modelcontextprotocol.io](https://modelcontextprotocol.io)
- **MCP 协议规范**: [https://modelcontextprotocol.io/specification/2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18)
- **MCP GitHub 仓库**: [https://github.com/modelcontextprotocol/modelcontextprotocol](https://github.com/modelcontextprotocol/modelcontextprotocol)
- **JSON-RPC 2.0 规范**: [https://www.jsonrpc.org/specification](https://www.jsonrpc.org/specification)

### 项目内部文档

- [HTTP 传输层开发指南](./http-transport-guide.md)
- [SourceTextBuilder API 使用指南](./sourcetextbuilder-guide.md)
- [项目开发指南](../../.github/copilot-instructions.md)

## 🔍 快速检查清单

在提交代码前，请确保：

- [ ] 所有新增/修改的消息类型都有双语注释
- [ ] 中文注释使用中文标点符号
- [ ] 中文末尾有 `<br/>`，英文在新行
- [ ] 英文注释与官方 schema 原文一致
- [ ] 文件命名遵循项目约定
- [ ] 继承关系正确（RequestParams/Result）
- [ ] `_meta` 字段注释包含官方文档链接
- [ ] 代码无编译错误和警告
- [ ] 已更新本文档中的消息类型清单（如有新增）
