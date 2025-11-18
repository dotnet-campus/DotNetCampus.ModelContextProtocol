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

### Quick Start

A typical MCP server program looks like this:

```csharp
internal class Program
{
    private static async Task Main(string[] args)
    {
        // The server name and version will be sent to clients via the MCP protocol
        var mcpServer = new McpServerBuilder("Sample Server", "1.0.0")
            .WithTools(t => t
                // If your MCP tool parameters and return values use custom types, you need to provide a JSON serialization context
                .WithJsonSerializer(McpToolJsonContext.Default)
                // Register various MCP tools
                .WithTool(() => new SampleTools())
                .WithTool(() => new SampleTools2())
            )
            // Use Streamable HTTP transport, listening on http://localhost:5943/mcp
            // Also compatible with SSE, listening on http://localhost:5943/mcp/sse
            .WithHttp(5943, "mcp")
            // You can also use stdio (standard input/output) transport, which is recommended by the MCP protocol for all MCP servers
            // However, it's generally not recommended to enable both http and stdio simultaneously,
            // as the former typically requires singleton execution while the latter must support multiple instances
            // .WithStdio()
            .Build();
#if DEBUG
        // Enable debug mode so that when the MCP server encounters exceptions, it returns exception information to clients for easier debugging
        // It's generally not recommended to enable this mode in production, as it would expose internal implementation details of the server
        mcpServer.EnableDebugMode();
#endif
        // Run the MCP server
        await mcpServer.RunAsync();
    }
}

[JsonSerializable(typeof(Foo))]
[JsonSerializable(typeof(Bar))]
[JsonSourceGenerationOptions(
    // Recommended: Most MCP protocol implementations use camelCase naming
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    // Recommended: Most MCP protocol implementations use string enums
    UseStringEnumConverter = true,
    // Recommended: Cannot guarantee AI will always put metadata properties first
    AllowOutOfOrderMetadataProperties = true
    // If you plan to use less capable models, you can also enable the following options
    // PropertyNameCaseInsensitive = true,
    // NumberHandling = JsonNumberHandling.AllowReadingFromString
    )]
internal partial class McpToolJsonContext : JsonSerializerContext;
```

### Declaring MCP Tool Methods

```csharp
public class SampleTools
{
    /// <summary>
    /// A tool for AI debugging that echoes back information as-is
    /// </summary>
    /// <param name="text">The string to echo back</param>
    /// <returns>The echoed string</returns>
    [McpServerTool(ReadOnly = true)]
    public string Echo(string text)
    {
        return text;
    }
}
```

### Advanced Usage

For advanced usage including supported types for parameters and return values, type polymorphism, and more, please refer to the [Quick Start Guide](docs/quickstart/README.md)
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## About dotnet-campus

dotnet-campus（.NET 职业技术学院）
