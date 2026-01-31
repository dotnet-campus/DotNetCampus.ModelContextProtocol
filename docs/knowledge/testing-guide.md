# 单元测试与质量保证指南

本文档旨在指导 `DotNetCampus.ModelContextProtocol` 项目的测试工作，确保代码质量并符合 MCP 协议标准。

## 1. 测试范围设计

为了确保库的健壮性和协议符合性，我们需要覆盖以下层面的测试：

### 1.1 核心逻辑与集成 (Integration/End-to-End)
使用本库的 Server 和 Client 在同一进程内进行端到端交互。这是我们测试的重心。
*   **握手流程**: `Initialize` -> `Initialized`。
*   **主要能力测试**:
    *   **Tools**: ListTools, CallTool (包括参数验证, 异常处理, 长文本返回等)。
    *   **Resources**: ListResources, ReadResource, Subscription。
    *   **Prompts**: ListPrompts, GetPrompt。
*   **通知机制**: Logging, Progress, 资源变更通知。

### 1.2 传输层 (Transport Layer)
针对 `src/DotNetCampus.ModelContextProtocol/Transports` 及具体实现。
*   **多种实现覆盖**：
    *   **HTTP**: 覆盖 `LocalHost` 和 `TouchSocket` 实现。
    *   **Stdio**: 覆盖标准输入输出流传输（这是 MCP 最常用的模式）。
    *   **In-Process**: （规划中）直接在内存中传递消息，用于极速测试核心逻辑。
*   **会话管理**：
    *   连接建立 (Connection management)。
    *   消息分片/粘包处理 (针对 Stdio/Stream 传输)。
    *   Cancellation 和 Timeout 处理。

### 1.3 兼容性测试 (Compliance)
*   **官方服务验证**：
    *   验证本库 Client 能否正确连接并调用 **官方 MCP SDK (Node.js/Python)** 编写的 Server。
    *   验证本库 Server 能否被官方 MCP Client 连接。
*   **协议正确性**：通过与官方 SDK 的交互，间接验证 JSON 序列化/反序列化、协议字段定义的正确性（不再单独编写序列化测试）。

## 2. 测试规范

### 2.1 框架与工具
*   **测试框架**: MSTest
*   **断言库**: MSTest 自带 Assert
*   **Mock 策略**: 
    *   优先使用 **In-Process Integration Tests** (即真实的 Server + 真实的 Client)，因为 MCP 逻辑重在交互。
    *   对于难以触发的异常路径，可适当使用 Mock `ITransport` 或特定的 Fake Tool。

### 2.2 代码编写规范

#### 复用性设计
利用 `[DataRow]` 或 `[DynamicData]` 在不同配置下运行相同的测试逻辑。

**示例：同时测试多种 HTTP 实现**
```csharp
[TestMethod]
[DataRow(HttpTransportType.LocalHost)]
[DataRow(HttpTransportType.TouchSocket)]
public async Task ListTools(HttpTransportType transportType)
{
    await using var package = await TestMcpFactory.Shared.CreateSimpleHttpAsync(transportType);
    var result = await package.Client.ListToolsAsync();
    Assert.IsNotNull(result);
}
```

#### 命名规范
*   **测试类**: `{Feature}Tests` 或 `{Component}Tests`，例如 `ToolExecutionTests`.
*   **测试方法**: 使用简单清晰的方法名，配合 `DisplayName` 说明测试目的。必要时可使用后缀区分场景（如 `_Error`, `01` 等）。
    *   例: `CallTool` (配合 `[DataRow]`), `CallTool_Exception`
    *   避免过长的 `Method_State_Expected` 命名风格。

## 3. 测试辅助设施 (Test Infrastructure)

为了方便编写测试，我们需要完善 `DotNetCampus.ModelContextProtocol.Tests` 项目中的辅助工具。

### 3.1 扩充 `TestMcpFactory`
目前的 `TestMcpFactory` 需进一步扩充：

1.  **Fluent Configuration**: 允许测试用例自定义 Server 挂载的 Tools/Resources。
    ```csharp
    // 理想的测试代码写法
    var package = await TestMcpFactory.Create()
        .WithTools(tools => tools.Add("calculate", (int a, int b) => a + b))
        .UseTransport(transport => transport.InternalMemory) // 内存直连
        .BuildAsync();
    ```
2.  **特定的测试工具集 (Test Tools)**:
    *   创建多种预置的 Tool 供测试使用：
        *   `ExceptionTool`: 总是抛出异常，测试错误处理。
        *   `LongTextTool`: 返回超长文本，测试分片或性能。
        *   `EchoTool`: 原样返回输入，测试复杂对象传参。

### 3.2 传输层模拟方案

#### 内存传输 (In-Process Transport)
实现 `InProcessServerTransport` 和 `InProcessClientTransport`。
*   利用字符串 Key 或共享内存对象进行配对。
*   **优势**: 极快，无网络/IO开销，适合大量逻辑测试。
*   **未来**: 成熟后的代码可移入主库供用户使用。

#### Stdio 传输模拟
改造现有的 `StdioServerTransport` 和 `StdioClientTransport`。
*   **构造函数注入**: 允许传入 `Stream` (StandardInput/StandardOutput) 而非硬编码 Console。
*   **测试方式**: 在测试中使用 `MemoryStream` 或 `PipeStream` 连接 Server 和 Client 实例，无需启动外部子进程即可测试流式协议逻辑（如 Header 解析、粘包处理）。

### 3.3 官方 Server 启动器 (`OfficialServerFixture`)
编写一个帮助类，用于启动外部 Node.js 进程运行官方示例 Server。
*   **假设**: CI/开发环境已安装 `npm`/`npx`。
*   **功能**: 启动 `npx -y @modelcontextprotocol/server-filesystem`，并提供连接到该进程的标准输入输出的 Transport。

## 4. 建议的测试开发路线图 (Todo List)

### Phase 1: 核心功能验证 (Priority High)
覆盖 Client 调用 Server 的主流程。
- [ ] **Client/ToolTests.cs**:
    - [ ] 构造 `ExceptionTool` 等辅助工具。
    - [ ] 测试无参、简单参数、复杂对象参数的 Tool 调用。
    - [ ] 测试 Tool 执行抛出异常时，Client 端收到的结果（应包含 `isError: true`）。
- [ ] **Client/LifecycleTests.cs**:
    - [ ] 显式测试 `InitializeAsync` 和版本协商。
    - [ ] 测试 `PingAsync`。

### Phase 2: 传输层增强 (Priority Medium)
- [ ] **TestInfrastructure**:
    - [ ] 实现 `InProcessTransport` 并验证其可靠性。
    - [ ] 改造 Stdio Transport 支持 Stream 注入。
- [ ] **Transport/StdioTransportTests.cs**:
    - [ ] 使用内存流模拟 Stdio，验证 `StreamJsonRpc` 或自定义流读写的粘包/分片处理能力。

### Phase 3: 官方兼容性 (Priority Low/Validation)
- [ ] **Compliance/OfficialIntegrationTests.cs**:
    - [ ] 尝试连接官方 `filesystem` Server 并读取目录。
