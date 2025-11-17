# DotNetCampus.ModelContextProtocol

[![.NET Build and Test](https://github.com/dotnet-campus/DotNetCampus.ModelContextProtocol/actions/workflows/dotnet-core.yml/badge.svg)](https://github.com/dotnet-campus/DotNetCampus.ModelContextProtocol/actions/workflows/dotnet-core.yml)
[![NuGet](https://img.shields.io/nuget/v/DotNetCampus.ModelContextProtocol.svg)](https://www.nuget.org/packages/DotNetCampus.ModelContextProtocol)

A lightweight, zero-dependency yet full-featured MCP protocol implementation built with .NET. It can be easily integrated into your application, regardless of its architecture.

## Features

- 🚀 Lightweight and high-performance
- 📦 Zero external dependencies
- 🔌 Easy to integrate
- 🎯 Full MCP protocol support

## Getting Started

### Installation

```bash
dotnet add package DotNetCampus.ModelContextProtocol
```

### 初始化

一个典型的 MCP 服务器程序如下所示：

```csharp
internal class Program
{
    private static async Task Main(string[] args)
    {
        // 此服务器名和版本号会在 MCP 协议中发送给客户端
        var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
            .WithTools(t => t
                // 如果你的 MCP 工具参数和返回值存在自定义类型，需要传入 JSON 序列化上下文
                .WithJsonSerializer(McpToolJsonContext.Default)
                // 注册各种 MCP 工具
                .WithTool(() => new SampleTools())
                .WithTool(() => new SampleTools2())
            )
            // 传输层使用 Streamable HTTP，监听 http://localhost:5943/mcp，
            // 传输层同时兼容 SSE，监听地址为 http://localhost:5943/mcp/sse
            .WithHttp(5943, "mcp")
            // 传输层也可使用 stdio（标准输入输出），这是 MCP 协议建议所有 MCP 服务器都支持的传输层
            // 不过通常不建议同时启用 http 和 stdio，因为前者通常要求单例运行，后者则必须支持多实例运行
            // .WithStdio()
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

### 高级用法

关于参数和返回值支持的类型、类型多态等高级用法，请参阅 [快速使用文档](docs/quickstart/README.md)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## About dotnet-campus

dotnet-campus（.NET 职业技术学院）
