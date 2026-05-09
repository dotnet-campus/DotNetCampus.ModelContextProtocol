# DotNetCampus.ModelContextProtocol

[![.NET Build and Test](https://github.com/dotnet-campus/DotNetCampus.ModelContextProtocol/actions/workflows/dotnet-build.yml/badge.svg)](https://github.com/dotnet-campus/DotNetCampus.ModelContextProtocol/actions/workflows/dotnet-build.yml) [![NuGet](https://img.shields.io/nuget/v/DotNetCampus.ModelContextProtocol.svg?label=DotNetCampus.ModelContextProtocol)](https://www.nuget.org/packages/DotNetCampus.ModelContextProtocol)

| [English][en] | [简体中文][zh-hans] | [繁體中文][zh-hant] |
| ------------- | ------------------- | ------------------- |

[en]: /docs/en/README.md
[zh-hans]: /docs/zh-hans/README.md
[zh-hant]: /docs/zh-hant/README.md

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

## Quick Start

### Server

```csharp
var mcpServer = new McpServerBuilder("Sample Mcp Server", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    .WithLocalHostHttp(5943, "mcp")
    .Build();

await mcpServer.RunAsync();

public class SampleTools
{
    /// <summary>
    /// 原样返回输入文本。
    /// </summary>
    /// <param name="text">要原样返回的字符串</param>
    [McpServerTool(ReadOnly = true)]
    public string EchoTool(string text)
    {
        return text;
    }
}
```

### Client

```csharp
var client = new McpClientBuilder("Sample Mcp Client", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

var arguments = JsonSerializer.SerializeToElement(new { text = "Hello, MCP!" });
var result = await client.CallToolAsync("echo_tool", arguments);

Console.WriteLine(result.Content);
```

## Documentation

See [docs/en/README.md](docs/en/README.md) for the full documentation index.

## Contributing

Contributions are welcome. Please feel free to submit a pull request.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

## About dotnet-campus

dotnet-campus（.NET 职业技术学院）
