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
- 如果参数是复杂的数据类型或枚举，不必在参数注释中详细描述每个内部属性或字段，因为本库会自动递归地提取注释，并将它们包含在 MCP 协议中发送给客户端
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
    ReadOnly = true              // 标记为只读工具，调用时不会修改其所在环境
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
- **Structured**：控制是否为此工具生成结构化输出（outputSchema + structuredContent）：
  - **未设置**：对于非空对象类型自动生成结构化输出；对于可空对象或对象集合类型产生编译错误 DM0102，要求显式设置为 `false`
  - **true**：显式启用结构化输出。仅对非空对象类型有效；对不可结构化的类型产生编译错误 DM0101，对可空对象和对象集合产生编译错误 DM0103
  - **false**：显式禁用结构化输出。对所有类型有效，不会产生 outputSchema

例如，返回自定义对象类型的工具默认生成结构化输出；若不需要，可显式禁用：

```csharp
[McpServerTool(ReadOnly = true, Structured = false)]
public LocalTimeInfo GetTime() { ... }
```

### 参数与上下文

#### 参数类型

工具方法支持多种参数类型。以下表格汇总了各类参数的标注方式、是否进入输入 Schema、以及运行时值的来源。

> 表中「—」表示不出现在输入 Schema 中。`[ToolParameter]` 还支持 `Name`（覆盖 JSON 属性名）和 `Description`（覆盖参数描述），各类型通用，表中不单独列出。

| 参数类型 | 标注方式 | 输入 Schema | 运行时取值 |
|---|---|---|---|
| `IMcpServerCallToolContext` | 自动 | — | 转发当前 `context` |
| JSON 可序列化类型（基本类型、string、对象等） | 自动 | 是 | `jsonArguments["name"]` 反序列化为 .NET 类型 |
| `JsonElement` / `object` | 自动 | 是（任意 JSON） | `jsonArguments["name"]` 原样传递 |
| 整个输入对象 `[ToolParameter(Type = InputObject)]` | 必须标注 | 是（展开为属性） | 整个 `jsonArguments` 反序列化 |
| DI 注入 `[ToolParameter(Type = Injected)]`（可空） | 必须标注 | — | `GetService()`；未注册→`null` |
| DI 注入 `[ToolParameter(Type = Injected)]`（非空） | 必须标注 | — | `GetService()`；未注册→`McpToolServiceNotFoundException` |
| `CancellationToken` | 自动 | — | `context.CancellationToken` |

标注方式为自动的，由源生成器根据参数类型自动推断。任何参数标注为 `InputObject` 后**不允许**再有任何普通 JSON 参数。标注为 `Injected` 的参数值由 `IServiceProvider` 提供，需在服务器初始化时配置 `WithServices()`，详见[依赖注入](DependencyInjection.md)。

> **💡 提示**：`CancellationToken` 和 `IMcpServerCallToolContext` 推荐放在参数列表末尾，避免影响 JSON 参数的可读性。

#### IMcpServerCallToolContext 上下文

`IMcpServerCallToolContext` 提供工具方法执行时的上下文信息，包括：

- 当前工具名称（`context.Name`）
- 原始 JSON 入参（`context.InputJsonArguments`）
- 请求中的 `_meta` 元数据（`context.Meta`），可用于分布式追踪等场景
- MCP 服务器信息（`context.McpServer.ServerName`）
- 传输层会话（`context.TransportSession`），所有传输层均可用，包含：
  - `SessionId`：会话 ID，可用于区分不同客户端连接（stdio 传输层下为 `null`）
  - `ConnectedClientInfo`：客户端在初始化握手时提供的信息（名称、版本等）
  - `ConnectedClientCapabilities`：客户端声明的能力
  - `NegotiatedProtocolVersion`：协商出的协议版本
- HTTP 传输层上下文（`context.HttpTransportContext`），仅在 HTTP 传输层下可用，包含：
  - `SessionId`：与 `TransportSession.SessionId` 相同
  - `Headers`：当前 HTTP 请求的请求头

> **💡 提示**：如果只需要区分不同客户端，推荐使用 `context.TransportSession`，它在所有传输层（HTTP、stdio、InProcess、IPC）下均可用。`context.HttpTransportContext` 仅在 HTTP 传输层下可用，适合需要读取 HTTP 请求头等场景。

> **⚠ 重要**：`IMcpServerCallToolContext` 实例**仅在当前工具方法执行期间有效**。不要将其存储到静态字段或跨异步边界传递，因为工具调用结束后上下文即失效。

#### 完整参数示例

以下示例展示了 `IMcpServerCallToolContext`、必需参数（无默认值）、可选参数（带默认值）综合用法：

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
    var session = context.TransportSession;
    var info = $"""
        Server name: {context.McpServer.ServerName}
        SessionId: {session?.SessionId}
        Client: {session?.ConnectedClientInfo?.Name} {session?.ConnectedClientInfo?.Version}
        Protocol: {session?.NegotiatedProtocolVersion}
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

（示例中 `EchoOptions`、`EchoExtraData`、`EchoResult` 的定义见下方 [辅助类型](#辅助类型)。此示例返回 `Task<EchoResult>`，`EchoResult` 为非空对象，默认启用结构化输出；返回值行为的完整规则见下方[返回值类型](#返回值类型)表格。）

### 返回值类型

方法的返回值类型决定了编译期源生成器如何处理返回值，以及运行时生成的 `CallToolResult` 结构。你可以通过 `Structured` 属性（参见上文）进一步控制是否生成 MCP 结构化输出。

以下表格汇总了所有支持的返回值类型及其行为。推荐等级含义：

- **推荐**：返回值行为完全符合 MCP 协议要求，无需任何转换
- **支持**：本库会对返回值进行加工，使其符合 MCP 协议要求
- **不推荐**：部分场景下可能返回不符合 MCP 协议的结果，可能导致某些 MCP 客户端异常
- **自行处理**：本库不干预返回值，由开发者完全控制协议层数据

> 表中「—」表示 Structured 不适用，设 `true`→DM0101。`只可 false` 表示必须显式设置，未设→DM0102，设 `true`→DM0103。

| 返回值类型 | 推荐等级 | Structured 设置 | 输出 Schema | 运行时行为 |
|---|---|---|---|---|
| `string` | 推荐 | — | — | TextContentBlock(text) |
| 非空自定义对象（record/class）`Foo` | 推荐 | 默认 true；可设 false 禁用 | 默认生成 OutputSchema | 默认：StructuredContent + TextContentBlock(json)；Structured=false 时仅 TextContentBlock(json) |
| `string?` | 支持 | — | — | TextContentBlock(text 或 "") |
| 可空基本类型 / 可空枚举（`int?`/`bool?`/`DayOfWeek?` 等） | 支持 | — | — | ToString()；null->"" |
| 基本类型 / 枚举（`int`/`bool`/`DayOfWeek` 等） | 支持 | — | — | ToString() |
| `JsonElement` | 支持 | — | — | TextContentBlock(json) |
| `void` / `Task` / `ValueTask` | 不推荐 | — | — | [] |
| 可空自定义对象（record/class）`Foo?` | 不推荐 | 只可 false | — | json->TextContentBlock；null->"" |
| 基本类型集合（`string[]`/`int[]`/`IReadOnlyList<DayOfWeek>` 等） | 不推荐 | — | — | 逐元素 ToString()->多个块；null/空->[] |
| 对象集合 `Foo[]` / `IReadOnlyList<Foo>` | 不推荐 | 只可 false | — | 逐元素 json->多个块；null/空->[] |
| `CallToolResult` | 自行处理 | — | — | 原样返回 |

方法可以是同步或异步的：

- 同步：可使用上述所有种类的返回值类型
- 异步：可使用 `Task`、`Task<T>`、`ValueTask` 和 `ValueTask<T>` 的异步返回值。其中 `T` 即为表中对应的返回值类型，行为一致

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
    return text; // string 可隐式转换为 CallToolResult
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

当你的工具参数或返回值使用自定义类型时，需要传入 JSON 序列化上下文以支持 AOT 编译。如果你希望工具类支持依赖注入，则传入 `IServiceProvider` 实例。MCP 库的依赖注入由编译期源生成器实现，零反射。详见[依赖注入](DependencyInjection.md)。

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
