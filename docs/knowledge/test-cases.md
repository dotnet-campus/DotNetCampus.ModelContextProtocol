# 测试用例规划

本文档详细列出了 `DotNetCampus.ModelContextProtocol` 仓库应实现的单元测试用例，按功能模块和测试文件进行组织。

> **状态图例**: ✅ 已实现 | ⏳ 占位（待功能完成） | 📋 规划中

## 1. 核心功能测试 (Core Integration)

**文件路径**: `tests/DotNetCampus.ModelContextProtocol.Tests/Clients/CoreTests.cs`
**目标**: 使用 HTTP 传输（LocalHost 和 TouchSocket）验证 Client 与 Server 的完整交互流程。这是测试覆盖率的主力。

### 1.1 初始化与握手 (Handshake)
| 状态 | 方法名 | DataRow / 参数 | 预期行为 |
| :---: | :--- | :--- | :--- |
| ✅ | `Initialize` | `LocalHost`, `TouchSocket` | 成功握手，返回 ServerInfo 和 Capabilities |
| ✅ | `Ping` | `LocalHost`, `TouchSocket` | 连接状态正常，IsConnected 为 true |

### 1.2 工具调用 (Tools)
**测试前提**: Server 端注册了 Calculator (`add`/`divide`), Echo (`echo`/`echo_user`), LongText (`generate`), Exception (`throw_error`/`throw_nested`) 等测试工具。

> **注意**: 工具名称使用 snake_case 格式（由源生成器自动从 PascalCase 方法名转换）。

| 状态 | 方法名 | DataRow / 参数 | 预期行为 |
| :---: | :--- | :--- | :--- |
| ✅ | `ListTools` | `LocalHost`, `TouchSocket` | 返回工具列表，验证包含 `add`, `echo`, `throw_error`, `generate` |
| ✅ | `CallTool` | `Tool="add", Args={a=10, b=20}` | 返回 `content[0].text == "30"`, `isError == false` |
| ✅ | `CallTool_ComplexObject` | `Tool="echo_user", Args={user={Name="TestUser", Id=123}}` | 能正确传递并返回复杂 JSON 对象 |
| ✅ | `CallTool_MissingArgs` | `Tool="add", Args={a=10}` | 返回 `isError == true`, message 包含参数缺失提示 |
| 📋 | `CallTool_InvalidArgType` | `Tool="add", Args={a="bad", b=2}` | 返回 `isError == true`, message 包含类型错误提示 |
| ✅ | `CallTool_ImplementationThrows` | `Tool="throw_error"` | 返回 `isError == true`, message 包含异常信息 |
| ✅ | `CallTool_LongOutput` | `Tool="generate", Length=100KB` | 成功接收完整长文本，无截断或内存溢出 |
| ✅ | `CallTool_NonExistent` | `Tool="non_existent_tool"` | 返回 `isError == true`，提示工具不存在 |
| ✅ | `CallTool_Echo` | `Tool="echo", Args={message="Hello"}` | 返回原始消息 |

### 1.3 资源访问 (Resources)
**测试前提**: Server 端注册了 `SimpleResource` 测试资源提供者。

| 状态 | 方法名 | DataRow / 参数 | 预期行为 |
| :---: | :--- | :--- | :--- |
| ✅ | `ListResources` | `LocalHost`, `TouchSocket` | 返回资源列表，验证包含 `test://file1`, `test://image.png` |
| ✅ | `ReadResource_TextFile` | `Uri="test://file1"` | 返回 TextResourceContents, 验证内容正确 |
| ✅ | `ReadResource_BinaryFile` | `Uri="test://image.png"` | 返回 BlobResourceContents, 验证 Base64 编码正确 |
| 📋 | `ReadResource_NonExistent` | `Uri="test://404"` | Server 返回错误响应 |
| ✅ | `ReadResource_WithTemplate` | `Uri="test://users/123/profile"` | 正确匹配 UriTemplate 并返回动态资源 |

### 1.4 提示词 (Prompts)
> ⚠️ **注意**: Server 端 Prompts 功能尚未完全实现，以下测试为占位符。

| 状态 | 方法名 | DataRow / 参数 | 预期行为 |
| :---: | :--- | :--- | :--- |
| ⏳ | `ListPrompts` | - | 返回 Prompts 列表 |
| ⏳ | `GetPrompt` | `Name="SimplePrompt"` | 返回 Messages 列表 |
| 📋 | `GetPrompt_WithArguments` | `Name="TemplatePrompt", Args={topic="Code"}` | 返回内容中包含替换后的 "Code" |

---

## 2. 传输层测试 (Transport Layer)

**文件路径**: `tests/DotNetCampus.ModelContextProtocol.Tests/Transports/TransportTests.cs`
**目标**: 验证不同传输实现的连接建立、断开及特殊传输特性。

### 2.1 协议无关性测试 (Polymorphism)
使用 `[DataRow]` 同时测试多种 Transport。

| 状态 | 方法名 | DataRow / 参数 | 预期行为 |
| :---: | :--- | :--- | :--- |
| ✅ | `Connect` | `LocalHost`, `TouchSocket` | HTTP 连接成功，IsConnected 为 true |
| ✅ | `Disconnect` | `LocalHost`, `TouchSocket` | 连接断开，Dispose 释放资源无异常 |
| 📋 | `Connect` | `StdioMock` | 内存流模拟连接成功 |
| 📋 | `Connect` | `InProcess` | 内存直连成功 |

### 2.2 Stdio 特性测试
**文件路径**: `tests/DotNetCampus.ModelContextProtocol.Tests/Transports/StdioTransportTests.cs`
**目标**: 针对流式传输特有的分包、粘包、Header 解析进行测试。

> ⚠️ **注意**: Stdio 传输层尚未完全实现，以下测试为占位符。

| 状态 | 方法名 | 场景描述 | 预期行为 |
| :---: | :--- | :--- | :--- |
| ⏳ | `Placeholder` | - | 占位测试 |
| 📋 | `Receive_ChunkedJson` | 将一个 JSON 报文拆成多个 byte 数组分次写入 Stream | 能够完整拼合包并解析出消息 |
| 📋 | `Receive_StickyPacket` | 将多个 JSON 报文一次性写入 Stream (粘包) | 能够依次触发多次消息接收逻辑 |
| 📋 | `Receive_InvalidHeader` | 写入错误的 `Content-Length` | 抛出协议异常或断开连接 |

### 2.3 HTTP 特性测试
**文件路径**: `tests/DotNetCampus.ModelContextProtocol.Tests/Transports/HttpTransportTests.cs`
**目标**: 针对 SSE 和 POST 通信模式的特定测试。

| 状态 | 方法名 | 场景描述 | 预期行为 |
| :---: | :--- | :--- | :--- |
| ✅ | `Delete_TerminateSession` | `LocalHost`, `TouchSocket` | DELETE 请求成功终止会话，IsConnected 为 false |
| ⏳ | `Post_NoSessionId` | 不带 `sessionId` query 发送消息 | 返回 400 Bad Request 或相应错误 |
| ⏳ | `Sse_EndpointEvent` | 建立旧协议 SSE 连接 | 首先收到 `event: endpoint` 消息 |

---

## 3. 官方兼容性测试 (Compliance)

**文件路径**: `tests/DotNetCampus.ModelContextProtocol.Tests/Compliance/OfficialServerTests.cs`
**目标**: 启动真正的 Node.js MCP Server 验证本库 Client。

> ⚠️ **注意**: 需要 Stdio 传输层和 Node.js 环境，以下测试为占位符。

| 状态 | 方法名 | 场景描述 | 预期行为 |
| :---: | :--- | :--- | :--- |
| ⏳ | `Connect_ToFilesystemServer` | 启动 `@modelcontextprotocol/server-filesystem` | 成功 Initialize |
| ⏳ | `ListResources_FromFilesystem` | 配置 Server 访问特定测试目录 | 返回目录下的文件列表 |
| ⏳ | `ReadResource_FileContent` | 读取已知文本文件 | 内容与磁盘文件一致 |

---

## 4. 已实现的辅助工具

### 4.1 测试工具 (Test Tools)
**文件路径**: `tests/DotNetCampus.ModelContextProtocol.Tests/McpTools/`

| 文件 | 类名 | 工具方法 | 用途 |
| :--- | :--- | :--- | :--- |
| `CalculatorTool.cs` | `CalculatorTool` | `Add(int a, int b)`, `Divide(int a, int b)` | 基本计算测试 |
| `EchoTool.cs` | `EchoTool` | `Echo(string message)`, `EchoUser(EchoUserInfo user)` | 回显和复杂对象测试 |
| `ExceptionTool.cs` | `ExceptionTool` | `ThrowError(string? message)`, `ThrowNested()` | 异常处理测试 |
| `LongTextTool.cs` | `LongTextTool` | `Generate(int length)` | 大数据量测试 |
| `SimpleTool.cs` | `SimpleTool` | `SayHello()` | 最简单的工具 |

### 4.2 测试资源 (Test Resources)
**文件路径**: `tests/DotNetCampus.ModelContextProtocol.Tests/McpResources/`

| 文件 | 类名 | 资源方法 | 用途 |
| :--- | :--- | :--- | :--- |
| `SimpleResource.cs` | `SimpleResource` | `TextFile()`, `BinaryImage()`, `UserProfile(int userId)` | 基本资源访问测试 |

### 4.3 测试工厂 (Integration Factory)
**文件路径**: `tests/DotNetCampus.ModelContextProtocol.Tests/TestMcpFactory.cs`

| 方法 | 用途 |
| :--- | :--- |
| `CreateSimpleHttpAsync(HttpTransportType)` | 创建仅包含 SimpleTool 的测试包 |
| `CreateFullHttpAsync(HttpTransportType)` | 创建包含所有测试工具的测试包 |
| `CreateFullHttpWithResourcesAsync(HttpTransportType)` | 创建包含工具和资源的完整测试包 |
| `CreateHttpCoreAsync(HttpTransportType, Action<McpServerBuilder>)` | 完全自定义的测试包创建 |

### 4.4 JSON 序列化上下文
**文件路径**: `tests/DotNetCampus.ModelContextProtocol.Tests/McpTools/TestToolJsonContext.cs`

用于 AOT 兼容的复杂对象序列化，包含 `EchoUserInfo` 等类型的注册。

---

## 5. 待开发的辅助工具

1.  **Mock Transport**
    *   `InProcessServerTransport` / `InProcessClientTransport`
    *   `MemoryStreamServerTransport` (用于 Stdio 模拟)

2.  **更多测试资源**
    *   `DictionaryResourceProvider`: 基于 `Dictionary<Uri, string>` 的简单资源提供者

---

## 6. 测试统计

| 类别 | 通过 | 跳过 | 规划 |
| :--- | :---: | :---: | :---: |
| 核心功能测试 | 28 | 2 | 2 |
| 传输层测试 | 6 | 3 | 4 |
| 官方兼容性测试 | 0 | 3 | 0 |
| **总计** | **34** | **8** | **6** |
