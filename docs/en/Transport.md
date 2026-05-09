# Transport

The MCP transport layer only sends and receives JSON-RPC messages. Application code usually only needs to choose the same transport on the server and client.

## HTTP

[Server Quick Start](Server_QuickStart.md) and [Client Quick Start](Client_QuickStart.md) use the HTTP transport. The server calls `WithLocalHostHttp(5943, "mcp")`, and the client calls `WithHttp("http://localhost:5943/mcp")`.

## stdio

stdio is useful when the client starts the server process.

Server:

```csharp
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Servers;

var mcpServer = new McpServerBuilder("StdioServer", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new CalculatorTools()))
    .WithStdio()
    .Build();

await mcpServer.RunAsync();

public class CalculatorTools
{
    [McpServerTool(ReadOnly = true)]
    public int Add(int a, int b) => a + b;
}
```

Client:

```csharp
using DotNetCampus.ModelContextProtocol.Clients;

await using var mcpClient = new McpClientBuilder()
    .WithClientInfo("StdioClient", "1.0.0")
    .WithStdio("dotnet", ["run", "--project", "../MinimalMcpServer"])
    .Build();
```

## In-Process

In-Process is useful for same-process embedding and integration tests. Start the server first. The client connects on its first request.

```csharp
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

var mcpServer = new McpServerBuilder("InProcessServer", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new CalculatorTools()))
    .WithInProcess()
    .Build();

await mcpServer.StartAsync();
try
{
    await using var mcpClient = new McpClientBuilder()
        .WithClientInfo("InProcessClient", "1.0.0")
        .WithInProcess(mcpServer)
        .Build();

    var arguments = JsonSerializer.SerializeToElement(new { a = 1, b = 2 });
    var result = await mcpClient.CallToolAsync("add", arguments);

    Console.WriteLine(result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text);
}
finally
{
    await mcpServer.StopAsync();
}

public class CalculatorTools
{
    [McpServerTool(ReadOnly = true)]
    public int Add(int a, int b) => a + b;
}
```

## IPC

The IPC transport is provided by the `DotNetCampus.ModelContextProtocol.Ipc` package. It is useful for communication between different processes on the same machine.

```bash
dotnet add package DotNetCampus.ModelContextProtocol.Ipc
```

Server:

```csharp
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Servers;

var mcpServer = new McpServerBuilder("IpcServer", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new CalculatorTools()))
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();

await mcpServer.RunAsync();

public class CalculatorTools
{
    [McpServerTool(ReadOnly = true)]
    public int Add(int a, int b) => a + b;
}
```

Client:

```csharp
using DotNetCampus.ModelContextProtocol.Clients;

await using var mcpClient = new McpClientBuilder()
    .WithClientInfo("IpcClient", "1.0.0")
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();
```

## Custom transports

A client transport implements `IClientTransport` and uses `IClientTransportManager` for JSON-RPC serialization and response dispatching.

```csharp
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Transports;

await using var mcpClient = new McpClientBuilder()
    .WithClientInfo("CustomClient", "1.0.0")
    .WithTransport(manager => new MyClientTransport(manager))
    .Build();

public sealed class MyClientTransport(IClientTransportManager manager) : IClientTransport
{
    public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        // Connect to your underlying channel here.
        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        // Close the underlying channel here.
        return ValueTask.CompletedTask;
    }

    public async ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await manager.WriteMessageAsync(stream, message, cancellationToken);
        await SendToConnectionAsync(stream.ToArray(), cancellationToken);
    }

    public ValueTask DisposeAsync() => DisconnectAsync();

    private static ValueTask SendToConnectionAsync(byte[] payload, CancellationToken cancellationToken)
    {
        // Write payload to your underlying channel.
        return ValueTask.CompletedTask;
    }
}
```

A server transport implements `IServerTransport`. After receiving a request, call `IServerTransportManager.ReadMessageAsync` to parse it, `HandleRequestAsync` to pass it to the MCP server, and `WriteMessageAsync` to write the response. If the transport supports server-initiated requests to the client, implement `IServerTransportSession` per connection.
