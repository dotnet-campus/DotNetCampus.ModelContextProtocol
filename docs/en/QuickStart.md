# Quick Start

For detailed explanation of the MCP protocol, see the [official MCP documentation](https://modelcontextprotocol.io/docs/getting-started/intro).

## Server

### Initialization

A typical MCP server program looks like this:

```csharp
internal class Program
{
    private static async Task Main(string[] args)
    {
        // The server name and version are sent to the client via the MCP protocol
        var mcpServer = new McpServerBuilder("Example Server", "1.0.0")
            // If your MCP tool parameters and return values use custom types, you need to provide a JSON serialization context
            .WithJsonSerializer(McpToolJsonContext.Default)
            .WithTools(t => t
                // Register various MCP tools
                .WithTool(() => new SampleTools())
                // .WithTool(() => new SampleTools2())
            )
            // Use Streamable HTTP transport, listening on http://localhost:5943/mcp
            .WithLocalHostHttp(5943, "mcp")
            // You can also use stdio (standard input/output), which is the transport layer that MCP recommends all servers support
            // However, it is generally not recommended to enable both http and stdio simultaneously, because the former typically requires singleton operation, while the latter must support multi-instance operation
            // .WithStdio()
            .Build();

        // Run the MCP server
        await mcpServer.RunAsync();
    }
}

[JsonSerializable(typeof(Foo))]
[JsonSerializable(typeof(Bar))]
[JsonSourceGenerationOptions(
    // Recommended: mainstream MCP protocol implementations use camelCase
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    // Recommended: mainstream MCP protocol implementations use string enums
    UseStringEnumConverter = true,
    // Recommended: there is no guarantee that AI will always place metadata properties first
    AllowOutOfOrderMetadataProperties = true
    // If you plan to use a less capable model, you can also enable the following options
    // PropertyNameCaseInsensitive = true,
    // NumberHandling = JsonNumberHandling.AllowReadingFromString
    )]
internal partial class McpToolJsonContext : JsonSerializerContext;
```

### MCP Tool Method Declaration

```csharp
public class SampleTools
{
    /// <summary>
    /// A debugging tool for AI that echoes back some information as-is.
    /// </summary>
    /// <param name="text">The string to echo back.</param>
    /// <returns>The echoed string.</returns>
    [McpServerTool(ReadOnly = true)]
    public string Echo(string text)
    {
        return text;
    }
}
```

For a complete explanation of method parameters, return value types, and sync/async behavior, see [Tools - Parameters and Return Values](Tools.md#parameters-and-context).

## Client

### Preparing the MCP Server

1. You can prepare an stdio transport MCP server
    - e.g. `npx -y @modelcontextprotocol/server-everything stdio` (no need to start it in advance)
2. Or prepare an HTTP transport MCP server
    - e.g. `npx -y @modelcontextprotocol/server-everything streamableHttp` (must be started in advance)

```powershell
npx -y @modelcontextprotocol/server-everything streamableHttp
Starting Streamable HTTP server...
MCP Streamable HTTP Server listening on port 3001
```

You can also use an MCP server written with this library, such as the one created by the example code in the previous section of this guide.

### Initialization

A typical MCP client program looks like this:

```csharp
/// <summary>
/// A class in the MCP host program for managing MCP clients.
/// </summary>
internal class McpManager
{
    public McpClient CreateMcpClient()
    {
        // The client name and version are sent to the server via the MCP protocol
        var mcpClient = new McpClientBuilder("Example Client", "1.0.0")
            // Connect to an stdio server (no need to start it in advance)
            .WithStdio("npx", ["-y", "@modelcontextprotocol/server-everything", "stdio"])
            // Or connect to an HTTP server (must be started in advance)
            // .WithHttp("http://localhost:3001/mcp")
            // Per official MCP protocol requirements, a single client can only connect to one MCP server
            .Build();
        return mcpClient;
    }
}
```

```csharp
// Optional call to ensure the client is connected to the server. If not called, the client will auto-connect on the first API call.
// The benefit of calling it early is that you can uniformly catch connection exceptions, filtering out broken MCP servers early to avoid affecting subsequent business logic.
await mcpClient.EnsureConnectedAsync();

var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools.Tools)
{
    Console.WriteLine(tool.Name);
}

// Call a tool, passing the tool name and arguments. If AOT is enabled, the arguments can use a JsonElement generated from your own JsonSerializerContext.
var result = await mcpClient.CallToolAsync("echo", JsonSerializer.SerializeToElement(new { text = "Hello, World!" }));
Console.WriteLine(result.Content);
```

> A single client connects to only one MCP server. In agent programs, you typically need to manage multiple MCP servers simultaneously. See [McpServerManager](McpServerManager.md).
