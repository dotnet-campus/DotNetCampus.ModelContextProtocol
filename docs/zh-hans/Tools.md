# Tools

Tools 用于让客户端请求服务端执行动作。我们假设你在阅读本文前，已经完成了 [快速开始](QuickStart.md) 中 MCP 服务器和客户端的搭建。

## 服务端实现工具

### 简单示例

一个简单的 MCP 工具实现如下：

```csharp
public class SampleTools
{
    /// <summary>
    /// 用于给 AI 调试使用的工具，原样返回一些信息
    /// </summary>
    /// <param name="text">要原样返回的字符串</param>
    /// <returns>原样返回的字符串</returns>
    [McpServerTool(ReadOnly = true)]
    public string EchoTool(string text)
    {
        return text;
    }
}
```

在这个示例中：

- 注释会成为此工具描述的重要部分，并在 MCP 协议中发送给客户端，所以写好注释对大模型正确使用工具非常重要；另外，不用太在意注释的多语言问题，因为大模型不在乎你用什么语言描述工具
- 如果参数是复杂的数据类型或枚举，不必详细在参数注释中详细描述每个内部属性或字段，因为本库会自动递归地提取注释，并将它们包含在 MCP 协议中发送给客户端
- 工具的名称默认使用 snake_case 命名法转换方法名，本例中，你会得到 `echo_tool`

### 自定义工具属性

`[McpServerTool]` 特性支持多个属性，让你精细控制工具在 MCP 协议中的行为和元数据：

```csharp
/// <summary>
/// 用于给 AI 调试使用的工具，原样返回一些信息
/// </summary>
/// <param name="text">要原样返回的字符串</param>
[McpServerTool(
    Name = "echo_tool",          // 指定工具名称，避免方法名与工具名强绑定（例如避免 Async 后缀影响工具名）
    Title = "原样输出",          // 给人类阅读的工具名称，AI 看不到，可用于 UI 展示
    Description = "用于给 AI 调试使用的工具，原样返回一些信息",  // 覆盖方法注释中的描述
    Idempotent = true,           // 标记为幂等工具，客户端可以安全地重试调用
    OpenWorld = false,           // 标记此工具不会与外部开放世界交互
    ReadOnly = true              // 标记为只读工具，调用时不会修改其环境
)]
public string EchoCustomized(string text)
{
    return text;
}
```

各属性说明：

- **Name**：工具在 MCP 协议中的名称。不指定时使用方法的 snake_case 命名。
- **Title**：人类可读的工具标题，AI 看不到，可用于 UI 展示。
- **Description**：工具描述，会覆盖方法 XML 注释中的描述。
- **Idempotent**：标记工具是否幂等。幂等工具在网络异常时可由客户端安全重试。
- **OpenWorld**：标记工具是否会与外部开放世界交互（如访问网络 API）。
- **ReadOnly**：标记工具是否只读。只读工具在调用时不会修改其所在环境。

### 参数与上下文

#### 隐式参数类型

工具方法可以接收以下隐式参数，按需添加在方法签名中即可：

- 任意可被 JSON 反序列化的类型（基本类型、数组、对象等）—— 由 MCP 客户端传入
- `CancellationToken` —— 当客户端取消工具调用时触发，推荐总是作为最后一个参数声明
- `IMcpServerCallToolContext` —— 提供当前工具调用的上下文信息
- `JsonElement` —— 接收任意 JSON 数据，适合参数结构不确定的场景

#### 显式参数类型（`[ToolParameter]` 特性）

通过 `[ToolParameter]` 特性标记特殊参数行为：

- `[ToolParameter(Type = ToolParameterType.InputObject)]`：此参数负责接收整个工具调用的输入对象（反序列化到一个类型）。使用此标记后**不允许**再有其他普通参数。
- `[ToolParameter(Type = ToolParameterType.Injected)]`：此参数由依赖注入框架自动注入，不由 MCP 协议层传入。需要已在服务器初始化时配置 `IServiceProvider`。

#### IMcpServerCallToolContext 上下文

`IMcpServerCallToolContext` 提供工具方法执行时的上下文信息，包括：

- 当前工具名称（`context.Name`）
- 原始 JSON 入参（`context.InputJsonArguments`）
- 请求中的 `_meta` 元数据（`context.Meta`），可用于分布式追踪等场景
- MCP 服务器信息（`context.McpServer.ServerName`）
- HTTP 传输层上下文（`context.HttpTransportContext`，含 SessionId、Headers 等）

> **⚠ 重要**：`IMcpServerCallToolContext` 实例**仅在当前工具方法执行期间有效**。不要将其存储到静态字段或跨异步边界传递，因为工具调用结束后上下文即失效。

#### 完整参数示例

以下示例展示了 `IMcpServerCallToolContext`、带默认值的参数、可为 null 的参数的综合用法：

```csharp
/// <summary>
/// 用于给 AI 调试使用的工具，原样返回一些信息
/// </summary>
/// <param name="text">要原样返回的字符串</param>
/// <param name="options">如何返回字符串</param>
/// <param name="count">要返回的字符串次数</param>
/// <param name="extraData">无意义的额外信息</param>
[McpServerTool(Name = "echo_tool")]
public Task<EchoResult> EchoAsync(
    IMcpServerCallToolContext context,
    string text,
    EchoOptions options = EchoOptions.JsonObject,
    int count = 1,
    EchoExtraData? extraData = null)
{
    var info = $"""
        Server name: {context.McpServer.ServerName}
        SessionId: {context.HttpTransportContext?.SessionId}
        Headers: {string.Join(", ", context.HttpTransportContext?.Headers)}
        InputJsonArguments: {context.InputJsonArguments}
        """;
    var result = $"""
        Echoing text: {text}
        Options: {options}
        Count: {count}
        ExtraData: {extraData}
        """;
    return Task.FromResult(new EchoResult { Info = info, Result = result });
}
```

（示例中 `EchoOptions`、`EchoExtraData`、`EchoResult` 的定义见下方 [辅助类型](#辅助类型)。）

### 返回值类型

方法的返回值可以是以下类型：

- `string`：返回给 AI 的字符串（通常是可被 AI 理解的自然语言）
- `void`：没有返回值。**请注意**，虽然这是 MCP 协议支持的类型，但有些 MCP 客户端会在服务器返回空结果时出现异常；此时建议改为 `string` 返回值，返回空字符串
- 任意可被 JSON 序列化的类型（根据 MCP 协议规范，**返回值只能是对象类型**，不能是数组或原始类型）
- `CallToolResult`：通用的工具调用结果，即 MCP 协议层的最终数据结构。使用此返回值类型，你可以直接在协议层控制返回给 AI 的数据
- `CallToolResult<T>`：带有结构化数据类型的工具调用结果，通过 `CallToolResult<T>.FromResult(result)` 方法创建实例。`T` 是任意可被 JSON 序列化的类型。使用此返回值类型，你在保持结构化返回值功能的同时，仍然具备协议层控制返回数据的能力

**特别的**，当返回值是可被 JSON 序列化的对象时，按 MCP 协议规范，我们会返回结构化数据，并在普通字符串返回值中也包含此数据的 JSON 序列化字符串（以供兼容）。同时此工具还会被标记为「具有结构化返回值」。

### 同步与异步

方法可以是同步或异步的：

- 同步：支持上述所有种类的返回值类型
- 异步：支持 `Task`、`Task<T>`、`ValueTask` 和 `ValueTask<T>` 的异步返回值

### 工具如何报告错误

工具可以通过两种方式向客户端报告错误，选择哪种取决于你的场景：

#### 方式一：抛出 `McpToolUsageException`

适合"用户用错了这个工具"的场景，例如参数不合法。抛出后 MCP 协议层会自动向客户端返回 `isError: true`，无需改变返回值类型：

```csharp
[McpServerTool]
public string Echo(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        throw new McpToolUsageException("text 参数不能为空。");
    }
    return text;
}
```

#### 方式二：返回 `CallToolResult.FromError()`

适合需要精确控制返回内容或返回结构化错误信息的场景：

```csharp
[McpServerTool]
public CallToolResult SafeEcho(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return CallToolResult.FromError("text 参数不能为空。");
    }
    return text;
}
```

两种方式的区别：

| | `McpToolUsageException` | `CallToolResult.FromError()` |
|---|---|---|
| 返回值类型 | 任意类型（不改变签名） | 必须为 `CallToolResult` |
| 适用场景 | 快速失败，不继续执行 | 需要结构化错误信息或精确控制返回格式 |
| 优点 | 简单直接，代码改动最小 | 灵活性最高 |

### 辅助类型

以下是上文中复杂示例用到的辅助类型定义：

```csharp
/// <summary>
/// 如何返回字符串
/// </summary>
public enum EchoOptions
{
    /// <summary>
    /// 以纯文本形式返回
    /// </summary>
    PlainText,

    /// <summary>
    /// 以 JSON 对象形式返回
    /// </summary>
    JsonObject,
}

/// <summary>
/// 无意义的额外信息
/// </summary>
/// <param name="Data1">可供保存的第 1 个值</param>
public record EchoExtraData(string Data1)
{
    /// <summary>
    /// 可供保存的第 2 个值
    /// </summary>
    public string Data2 { get; init; } = "";
}

/// <summary>
/// 供 AI 调试使用的工具返回值
/// </summary>
public record EchoResult
{
    /// <summary>
    /// 供 AI 调试使用的上下文信息
    /// </summary>
    public string Info { get; init; } = "";

    /// <summary>
    /// 供 AI 调试使用的结果信息
    /// </summary>
    public string Result { get; init; } = "";
}
```

## 服务端高级初始化

### JSON 序列化与依赖注入

当你的工具参数或返回值使用自定义类型时，需要传入 JSON 序列化上下文以支持 AOT 编译。如果你希望工具类支持依赖注入，则传入 `IServiceProvider` 实例：

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    // 传入 JSON 序列化上下文（AOT 兼容）
    .WithJsonSerializer(McpToolJsonContext.Default)

    // 传入 IServiceProvider，支持工具类的构造函数注入
    // 以及工具方法参数上的 [ToolParameter(Type = ToolParameterType.Injected)] 注入
    .WithServices(appServiceProvider)

    .WithTools(t => t
        // 普通注册：每次调用创建新实例
        .WithTool(() => new SampleTools())
        // 依赖注入注册：由 IServiceProvider 管理生命周期（必须已配置 WithServices）
        .WithTool<SampleTools2>()
    )

    .WithLocalHostHttp(5943, "mcp")
    .Build();
```

### 日志集成

将 MCP 服务器的内部日志桥接到你自己的日志系统，以便了解服务器的工作健康状况：

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    // 第二个参数控制传输层原始消息的日志详细级别，默认不记录
    .WithLogger(new McpLoggerBridge(myLogger), McpTransportRawMessageLoggingDetailLevel.Trimmed)
    // ... 其他配置
    .Build();
```

日志桥接实现参考：

```csharp
internal class McpLoggerBridge(ILogger logger) : IMcpLogger
{
    public bool IsEnabled(LoggingLevel loggingLevel)
    {
        return logger.IsEnabled(loggingLevel.ToLogLevel());
    }

    public void Log<TState>(LoggingLevel loggingLevel, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        logger.Log(loggingLevel.ToLogLevel(), default, state, exception, formatter);
    }
}
```

### 请求拦截

通过继承 `McpServerRequestHandlers` 并重写方法，你可以拦截发往此 MCP 服务器的所有请求，进行统一处理：

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    .WithRequestHandlers(s => new CustomRequestHandlers(s))
    // ... 其他配置
    .Build();
```

拦截器实现参考：

```csharp
internal class CustomRequestHandlers(McpServer server) : McpServerRequestHandlers(server)
{
    public override async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> rawRequest,
        string? toolName, IMcpServerTool? tool, IMcpServerCallToolContext? context)
    {
        var result = await base.CallToolAsync(rawRequest, toolName, tool, context);
        if (result.RawException is { } exception)
        {
            // 在工具调用异常时做额外的日志记录或告警
            Log.Error("工具调用异常", exception);
        }
        return result;
    }
}
```

### 传输层选择

本库支持多种传输层。选择哪种取决于你的部署场景：

| 传输层 | 方法 | 适用场景 |
|--------|------|---------|
| Streamable HTTP（内置） | `.WithLocalHostHttp()` | 本机通信，轻量零依赖 |
| Streamable HTTP（TouchSocket） | `.WithTouchSocketHttp()` | 需要公网监听，高性能 HTTP |
| stdio | `.WithStdio()` | 标准输入输出，MCP 官方推荐 |
| dotnetCampus.Ipc | `.WithDotNetCampusIpc()` | 本机高性能 IPC |

详细说明和配置方法请参阅 [选择传输层](Transport.md)。

## 客户端调用工具

### 客户端构建器重载

`McpClientBuilder` 的 `WithHttp` 和 `WithStdio` 方法各有两个重载：简单重载适合快速体验，选项重载适合需要自定义配置的生产场景。

**HTTP 传输层：**

```csharp
// 简单重载：仅指定 URL
var client = new McpClientBuilder("示例客户端", "1.0.0")
    .WithHttp("http://localhost:3001/mcp")
    .Build();

// 选项重载：可配置自定义 HttpClient、超时等
var client = new McpClientBuilder("示例客户端", "1.0.0")
    .WithHttp(new HttpClientTransportOptions
    {
        ServerUrl = "http://localhost:3001/mcp",
        HttpClient = customHttpClient,  // 可注入带认证头的 HttpClient
    })
    .Build();
```

**stdio 传输层：**

```csharp
// 简单重载：指定命令和参数
var client = new McpClientBuilder("示例客户端", "1.0.0")
    .WithStdio("npx", ["-y", "@modelcontextprotocol/server-everything", "stdio"])
    .Build();

// 选项重载：可配置环境变量
var client = new McpClientBuilder("示例客户端", "1.0.0")
    .WithStdio(new StdioClientTransportOptions
    {
        Command = "python",
        Arguments = ["-m", "my_mcp_server"],
        EnvironmentVariables = new Dictionary<string, string>
        {
            ["PYTHONPATH"] = "/path/to/modules",
        },
    })
    .Build();
```

### 基础调用

单个 MCP 客户端的典型调用流程：

```csharp
var client = new McpClientBuilder("示例客户端", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

// 可选：提前连接，统一捕获异常
await client.EnsureConnectedAsync();

// 列出工具
var tools = await client.ListToolsAsync();
foreach (var tool in tools.Tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

// 调用工具
var arguments = JsonSerializer.SerializeToElement(new { text = "Hello" });
var result = await client.CallToolAsync("echo_tool", arguments);
Console.WriteLine(result.Content);
```

### 多服务器管理（智能体场景）

在智能体程序中，通常需要同时管理多个 MCP 服务器（内置工具、外部服务、岗位程序等）。完整的 MCP 服务器管理器示例请参阅 [McpServerManager](McpServerManager.md)。
