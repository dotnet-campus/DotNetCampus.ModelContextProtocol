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

关于方法参数、返回值类型、同步/异步的完整说明，请参阅 [Tools](Tools.md)。

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

> 单个客户端只连接一个 MCP 服务器。在智能体程序中，通常需要同时管理多个 MCP 服务器，请参阅 [McpServerManager](McpServerManager.md)。
