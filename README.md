# DotNetCampus.ModelContextProtocol

[![.NET Build and Test](https://github.com/dotnet-campus/DotNetCampus.ModelContextProtocol/actions/workflows/dotnet-build.yml/badge.svg)](https://github.com/dotnet-campus/DotNetCampus.ModelContextProtocol/actions/workflows/dotnet-build.yml) [![NuGet](https://img.shields.io/nuget/v/DotNetCampus.ModelContextProtocol.svg?label=DotNetCampus.ModelContextProtocol)](https://www.nuget.org/packages/DotNetCampus.ModelContextProtocol)

| [English][en] | [简体中文][zh-hans] |
| ------------- | ------------------- |

[en]: /docs/en/README.md
[zh-hans]: /docs/zh-hans/README.md

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
var mcpServer = new McpServerBuilder("MinimalMcpServer", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new CalculatorTools()))
    .WithLocalHostHttp(5943, "mcp")
    .Build();

await mcpServer.RunAsync();

public class CalculatorTools
{
    [McpServerTool(ReadOnly = true)]
    public int Add(int a, int b)
    {
        return a + b;
    }
}
```

### Client

```csharp
await using var mcpClient = new McpClientBuilder()
    .WithClientInfo("MinimalMcpClient", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

var arguments = JsonSerializer.SerializeToElement(new { a = 10, b = 20 });
var result = await mcpClient.CallToolAsync("add", arguments);

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
