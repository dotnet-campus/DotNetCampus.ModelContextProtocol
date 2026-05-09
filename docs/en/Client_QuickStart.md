# Client Quick Start

Run the server from [Server Quick Start](Server_QuickStart.md) first.

## Create the project

```bash
dotnet new console -n MinimalMcpClient
cd MinimalMcpClient
dotnet add package DotNetCampus.ModelContextProtocol
```

## Program.cs

```csharp
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

await using var mcpClient = new McpClientBuilder()
    .WithClientInfo("MinimalMcpClient", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools.Tools)
{
    Console.WriteLine(tool.Name);
}

var arguments = JsonSerializer.SerializeToElement(new { a = 10, b = 20 });
var result = await mcpClient.CallToolAsync("add", arguments);
var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;

Console.WriteLine(text);
```

## Run

```bash
dotnet run
```
