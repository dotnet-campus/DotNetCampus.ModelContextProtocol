# Server Quick Start

## Create the project

```bash
dotnet new console -n MinimalMcpServer
cd MinimalMcpServer
dotnet add package DotNetCampus.ModelContextProtocol
```

## Program.cs

```csharp
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Servers;

var mcpServer = new McpServerBuilder("MinimalMcpServer", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new CalculatorTools()))
    // Listen on http://localhost:5943/mcp.
    .WithLocalHostHttp(5943, "mcp")
    .Build();

#if DEBUG
// Return tool exceptions to the client while debugging. Do not enable this in production.
mcpServer.EnableDebugMode();
#endif

await mcpServer.RunAsync();

public class CalculatorTools
{
    // The Add method is exposed to clients as the MCP tool named add.
    [McpServerTool(ReadOnly = true)]
    public int Add(int a, int b)
    {
        return a + b;
    }
}
```

## Run

```bash
dotnet run
```

After the server starts, clients can connect to `http://localhost:5943/mcp`.
