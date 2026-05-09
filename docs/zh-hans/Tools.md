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

### 完整示例

实际上，你可以对工具方法进行丰富而详细的自定义。以下是一个能体现大量功能的复杂示例：

```csharp
/// <summary>
/// 用于给 AI 调试使用的工具，原样返回一些信息
/// </summary>
/// <param name="text">要原样返回的字符串</param>
/// <param name="options">如何返回字符串</param>
/// <param name="count">要返回的字符串次数</param>
/// <param name="extraData">无意义的额外信息</param>
/// <param name="isError">如果希望工具直接报告错误，则传入 true</param>
/// <returns></returns>
[McpServerTool(
    Name = "echo_tool", // 通过在 Name 属性中指定工具名称，可以避免方法名与工具名的强绑定（例如避免 C# 惯用的 Async 后缀影响工具名）
    Title = "原样输出", // 这里的标题是给人类阅读的工具名称，AI 看不到
    Description = "用于给 AI 调试使用的工具，原样返回一些信息", // 这里的描述会覆盖方法注释中的描述
    Idempotent = true, // 标记此工具为幂等工具，客户端可以安全地重试调用
    OpenWorld = false, // 标记此工具不会与外部开放世界交互
    ReadOnly = true // 标记此工具为只读工具，调用时不会修改其环境
    )]
public Task<EchoResult> EchoAsync(
    IMcpServerCallToolContext context,
    string text,
    EchoOptions options = EchoOptions.JsonObject,
    int count = 1,
    EchoExtraData? extraData = null,
    bool isError = false)
{
    var info = $"""
        Server name: {context.McpServer.ServerName},
        SessionId: {context.HttpTransportContext?.SessionId},
        Headers: {string.Join(", ", context.HttpTransportContext?.Headers)},
        InputJsonArguments: {context.InputJsonArguments},
        """;
    var result = $"""
        Echoing text: {text}
        Options: {options}
        Count: {count}
        ExtraData: {extraData}
        """;
    if (isError)
    {
        throw new McpToolUsageException("这是你要求我报错的。");
    }
    return Task.FromResult(new EchoResult
    {
        Info = info,
        Result = result
    });
}
```

### 参数和返回值说明

方法参数可以是任意数量的，支持带默认值，支持以下类型：

- 隐式类型：
    - 任意可被 JSON 反序列化的类型（包括基本类型、数组和对象等）
    - `CancellationToken`: 表示取消令牌
    - `IMcpServerCallToolContext`: 表示当前工具方法的上下文信息
    - `JsonElement`：表示任意 JSON 数据
- 显式类型：
    - `[ToolParameter(Type = ToolParameterType.InputObject)]`：表示此参数负责接收整个工具调用的输入对象，此时不允许再有其他普通参数
    - `[ToolParameter(Type = ToolParameterType.Injected)]`：表示此参数由依赖注入框架自动注入，不由 MCP 协议层传入

方法的返回值可以是以下类型：

- `string`: 表示返回给 AI 的字符串（通常是可被 AI 理解的自然语言）
- `void`: 表示没有返回值 **请注意，虽然这是 MCP 协议支持的类型，但有些 MCP 客户端会在 MCP 服务器返回空结果时出现异常；此时建议改为 `string` 返回值，并返回空字符串**
- 任意可被 JSON 序列化的类型（根据 MCP 协议规范，**返回值只能是对象类型**，不能是数组或原始类型）
- `CallToolResult`: 通用的工具调用结果，这就是最终 MCP 协议层的数据结构；使用此返回值类型，你可以直接在 MCP 协议层控制返回给 AI 的数据
- `CallToolResult<T>`: 带有结构化数据类型的工具调用结果，通过 `CallToolResult.FromResult(result)` 方法创建实例，`T` 是任意可被 JSON 序列化的类型；使用此返回值类型，你可以在保证不破坏结构化返回值功能的同时，仍然具备在 MCP 协议层控制返回给 AI 的数据的能力

**特别的**，当返回值是可被 JSON 序列化的对象时，按 MCP 协议规范，我们会返回结构化数据，并在普通字符串返回值中也包含此数据的 JSON 序列化字符串（以供兼容）。同时此工具还会被标记为「具有结构化返回值」。

方法可以是同步或异步的：

- 支持上述所有种类的同步返回值
- 支持 `Task`、`Task<T>`、`ValueTask` 和 `ValueTask<T>` 的异步返回值

### 辅助类型

如果你希望上述复杂示例可正常工作，你可以参考下方定义的辅助类型：

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

### 高级初始化示例

有时应用程序更加复杂，初始化时需要使用更多功能：

```csharp
internal class Program
{
    private static async Task Main(string[] args)
    {
        var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
            // 如果你的 MCP 工具参数和返回值存在自定义类型，需要传入 JSON 序列化上下文
            // .WithJsonSerializer(McpToolJsonContext.Default)

            // 如果你希望 MCP 工具可被依赖注入，则在此传入 IServiceProvider 实例
            // 支持构造函数注入（直接支持），支持 MCP 工具方法参数注入（需要在方法参数上标记 `[ToolParameter(Type = ToolParameterType.Injected)]`）。
            .WithServices(appServiceProvider)

            // 将 MCP 服务器内部日志桥接到自己的日志系统中，以便能了解到 MCP 服务器的工作健康状况，方便调试。
            // 第二个参数可以指定 MCP 传输层原始消息的详细级别，默认不指定时不记录原始消息。
            .WithLogger(new McpLoggerBridge(/* 自己的日志接口 */), McpTransportRawMessageLoggingDetailLevel.Trimmed)

            // 包含大量拦截方法，可以拦截法向此 MCP 服务器的所有请求，进行统一处理或分不同功能处理。
            .WithRequestHandlers(s => new CustomRequestHandlers(s))

            // 注册各种 MCP 工具
            .WithTools(t => t
                // 注册各种 MCP 工具
                .WithTool(() => new SampleTools())
                // 只有指定了 IServiceProvider 时，下面这种依赖注入写法才可用（SampleTools2 构造函数注入）。
                // .WithTool<SampleTools2>()
            )

            // 如果你额外安装了 TouchSocket.Http 包，则本库会自动生成一个基于 TouchSocket.Http 的传输层实现，你可以直接启用它。
            // .WithTouchSocketHttp(new TouchSocketHttpServerTransportOptions
            // {
            //     Listen = ["0.0.0.0:5943", "[::]:5943"],
            //     EndPoint = "mcp",
            //     IsCompatibleWithSse = true,
            // })

            // 如果你只想使用本库内置的 Streamable HTTP 传输层，可以这样初始化，但本库自带的传输层只支持 localhost 监听。
            .WithLocalHostHttp(new LocalHostHttpServerTransportOptions
            {
                Port = 5943,
                EndPoint = "mcp",
                IsCompatibleWithSse = true,
            })

            // 传输层也可使用 stdio（标准输入输出），这是 MCP 协议建议所有 MCP 服务器都支持的传输层
            // .WithStdio()

            // 如果你额外安装了 dotnetCampus.Ipc 包，则本库会自动生成一个基于 dotnetCampus.Ipc 的传输层实现，你可以直接启用它，并复用已有的 IPC 服务。
            // .WithDotNetCampusIpc(ipcProvider)
            // 或者如果你没有线程的 IPC 服务，可以使用另一个重载创建一个独立的 IPC 服务。
            // .WithDotNetCampusIpc("McpIpcServer")

            .Build();

#if DEBUG
        // 启用调试模式，这样当 MCP 服务遇到异常时，会把异常信息返回给客户端，方便调试
        // 通常不建议在发布环境启用此模式，否则会暴露服务器的内部实现细节
        mcpServer.EnableDebugMode();
#endif

        // 运行 MCP 服务器
        await mcpServer.RunAsync();
    }
}

internal class McpLoggerBridge(ILogger logger) : IMcpLogger
{
    public bool IsEnabled(LoggingLevel loggingLevel)
    {
        return logger.IsEnabled(loggingLevel.ToLogLevel());
    }

    public void Log<TState>(LoggingLevel loggingLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        logger.Log(loggingLevel.ToLogLevel(), default, state, exception, formatter);
    }
}

internal class CustomRequestHandlers(McpServer server) : McpServerRequestHandlers(server)
{
    public override async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> rawRequest,
        string? toolName, IMcpServerTool? tool, IMcpServerCallToolContext? context)
    {
        var result = await base.CallToolAsync(rawRequest, toolName, tool, context);
        if (result.RawException is { } exception)
        {
            Log.Error("额外的异常记录", exception);
        }
        return result;
    }
}
```

## 客户端调用工具

一般来说，单独连接一个服务器的 MCP 客户端没有什么作用。通常是一个智能体程序，连接众多 MCP 服务器。

因此，大多数时候，我们要编写的都是一个 MCP 客户端管理器。

// 注：请参考工作区里「希沃白板小助手」文件夹这个仓库里的 `McpServerManager`，编写一个示例用的 MCP 客户端管理器。

```csharp
// 注：修改这里
// 请完善这个管理器，目标是让智能体开发者复制此类型后，能轻松管理多个 MCP 服务器，并且能方便地调用工具。
// 去掉大量不重要的代码，专注于极简示例。
// 在关键的地方添加注释，就像前面人类提供的代码和示例一样。
```