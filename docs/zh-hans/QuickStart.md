# 快速开始

关于 MCP 协议的详细说明，请参阅 [MCP 官方文档](https://modelcontextprotocol.io/docs/getting-started/intro)。

## 服务端

### 初始化

一个典型的 MCP 服务器程序如下所示：

```csharp
internal class Program
{
    private static async Task Main(string[] args)
    {
        // 此服务器名和版本号会在 MCP 协议中发送给客户端
        var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
            // 如果你的 MCP 工具参数和返回值存在自定义类型，需要传入 JSON 序列化上下文
            .WithJsonSerializer(McpToolJsonContext.Default)
            .WithTools(t => t
                // 注册各种 MCP 工具
                .WithTool(() => new SampleTools())
                // .WithTool(() => new SampleTools2())
            )
            // 传输层使用 Streamable HTTP，监听 http://localhost:5943/mcp
            .WithLocalHostHttp(5943, "mcp")
            // 传输层也可使用 stdio（标准输入输出），这是 MCP 协议建议所有 MCP 服务器都支持的传输层
            // 不过通常不建议同时启用 http 和 stdio，因为前者通常要求单例运行，后者则必须支持多实例运行
            // .WithStdio()
            .Build();

        // 运行 MCP 服务器
        await mcpServer.RunAsync();
    }
}

[JsonSerializable(typeof(Foo))]
[JsonSerializable(typeof(Bar))]
[JsonSourceGenerationOptions(
    // 建议设置：MCP 协议主流实现都使用驼峰命名法
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    // 建议设置：MCP 协议主流实现都使用字符串枚举
    UseStringEnumConverter = true,
    // 建议设置：无法保证 AI 永远将元数据属性放到首位
    AllowOutOfOrderMetadataProperties = true
    // 如果你计划使用很笨的模型，也可开启以下选项
    // PropertyNameCaseInsensitive = true,
    // NumberHandling = JsonNumberHandling.AllowReadingFromString
    )]
internal partial class McpToolJsonContext : JsonSerializerContext;
```

### MCP 工具方法声明

```csharp
public class SampleTools
{
    /// <summary>
    /// 用于给 AI 调试使用的工具，原样返回一些信息
    /// </summary>
    /// <param name="text">要原样返回的字符串</param>
    /// <returns>原样返回的字符串</returns>
    [McpServerTool(ReadOnly = true)]
    public string Echo(string text)
    {
        return text;
    }
}
```

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

## 客户端

### 准备 MCP 服务器

1. 你可以准备 stdio 传输层的 MCP 服务器
    - 如 `npx -y @modelcontextprotocol/server-everything stdio`（无需提前运行）
2. 也可以准备 http 传输层的 MCP 服务器
    - 如 `npx -y @modelcontextprotocol/server-everything streamableHttp`（需提前运行）

```powershell
npx -y @modelcontextprotocol/server-everything streamableHttp
Starting Streamable HTTP server...
MCP Streamable HTTP Server listening on port 3001
```

你也可以使用本库编写的 MCP 服务器，例如本文档前面章节示例代码创建的 MCP 服务器。

### 初始化

一个典型的 MCP 客户端代码如下所示：

```csharp
/// <summary>
/// MCP 主机程序中，用来管理 MCP 客户端的类
/// </summary>
internal class McpManager
{
    public McpClient CreateMcpClient()
    {
        // 此客户端名和版本号会在 MCP 协议中发送给服务器
        var mcpClient = new McpClientBuilder("示例客户端", "1.0.0")
            // 连接 stdio 服务器（无需提前运行）
            .WithStdio("npx", ["-y", "@modelcontextprotocol/server-everything", "stdio"])
            // 或者连接 http 服务器（需提前运行）
            // .WithHttp("http://localhost:3001/mcp")
            // 按照 MCP 官方协议要求，一个客户端只能连接一个 MCP 服务器
            .Build();
        return mcpClient;
    }
}
```

```csharp
// 可选调用，确保客户端已连接到服务器。如果不调用，客户端会在首次 API 调用时自动连接。
// 提前调用的好处是可以统一捕获连接异常，提前过滤掉坏掉的 MCP 服务器，避免影响后续业务逻辑。
await mcpClient.EnsureConnectedAsync();

var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools.Tools)
{
    Console.WriteLine(tool.Name);
}

// 调用工具，传入工具名和参数。如果开启了 AOT，后面的参数可以传入自己定义的 JsonSerializerContext 生成的 JsonElement。
var result = await mcpClient.CallToolAsync("echo", JsonSerializer.SerializeToElement(new { text = "Hello, World!" }));
Console.WriteLine(result.Content);
```
