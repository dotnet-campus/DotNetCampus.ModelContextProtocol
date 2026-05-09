# Transport

The MCP transport layer is responsible only for sending and receiving JSON-RPC messages. Business code usually only needs to select the same transport layer on both the server and client sides.

We assume you have already completed the MCP server and client setup described in [Quick Start](QuickStart.md) before reading this guide.

> **Core Principle**: `McpClientBuilder.Build()` only creates a client object and **does not trigger any I/O or network connections**. Connections are lazily triggered by `EnsureConnectedAsync` on the first API call. For details, see [Client "Build Before Connect" Principle](../knowledge/client-build-before-connect.md).

## HTTP

[Quick Start](QuickStart.md) uses the Streamable HTTP transport layer.

Server:

```csharp
var mcpServer = new McpServerBuilder("Example Server", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    // Listen on http://localhost:5943/mcp
    .WithLocalHostHttp(5943, "mcp")
    .Build();
```

Client:

```csharp
// Simple overload: specify URL only
var mcpClient = new McpClientBuilder("Example Client", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

// Options overload: configure custom HttpClient (auth headers, proxy, etc.)
var mcpClient = new McpClientBuilder("Example Client", "1.0.0")
    .WithHttp(new HttpClientTransportOptions
    {
        ServerUrl = "http://localhost:5943/mcp",
        HttpClient = customHttpClient,
    })
    .Build();
```

The built-in HTTP server transport layer only listens on localhost. If you need to listen on other addresses, use the TouchSocket HTTP transport layer provided by the `DotNetCampus.ModelContextProtocol.TouchSocket.Http` extension package.

## stdio

stdio is suitable for scenarios where the client starts the server process, and is the transport layer that the MCP specification recommends servers support.

Server:

```csharp
var mcpServer = new McpServerBuilder("Example Server", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // Send and receive MCP messages through standard input/output
    .WithStdio()
    .Build();

await mcpServer.RunAsync();
```

Client:

```csharp
// Simple overload: specify command and arguments
var mcpClient = new McpClientBuilder("Example Client", "1.0.0")
    // The client will start this command and communicate through its standard input/output
    .WithStdio("dotnet", ["run", "--project", "../MinimalMcpServer"])
    .Build();

// Options overload: configure environment variables
var mcpClient = new McpClientBuilder("Example Client", "1.0.0")
    .WithStdio(new StdioClientTransportOptions
    {
        Command = "dotnet",
        Arguments = ["run", "--project", "../MinimalMcpServer"],
        EnvironmentVariables = new Dictionary<string, string>
        {
            ["DOTNET_ENVIRONMENT"] = "Production",
        },
    })
    .Build();
```

## In-Process

In-Process is suitable for same-process embedding and integration testing. The server must be started first, and the client establishes a connection on the first request.

```csharp
var mcpServer = new McpServerBuilder("Embedded Server", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // Allow in-process MCP client connections
    .WithInProcess()
    .Build();

await mcpServer.StartAsync();
try
{
    await using var mcpClient = new McpClientBuilder("Embedded Client", "1.0.0")
        .WithInProcess(mcpServer)
        .Build();

    var arguments = JsonSerializer.SerializeToElement(new { text = "Hello" });
    var result = await mcpClient.CallToolAsync("echo_tool", arguments);

    Console.WriteLine(result.Content);
}
finally
{
    await mcpServer.StopAsync();
}
```

The In-Process transport layer does not provide process isolation — the server and client run in the same process with the same permissions. It is suitable for embedded scenarios and integration testing within trusted boundaries.

> For the definition of `SampleTools`, see [Tools - Basic Example](Tools.md#basic-example).

## IPC

The IPC transport layer is provided by the `DotNetCampus.ModelContextProtocol.Ipc` package, suitable for communication between different processes on the same machine.

```bash
dotnet add package DotNetCampus.ModelContextProtocol.Ipc
```

Server:

```csharp
var mcpServer = new McpServerBuilder("IPC Example Server", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // sample-mcp-pipe is the local pipe name; clients must use the same name to connect
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();

await mcpServer.RunAsync();
```

Client:

```csharp
await using var mcpClient = new McpClientBuilder("IPC Example Client", "1.0.0")
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();
```

## Custom Transport Layer

A client transport implements `IClientTransport` and uses `IClientTransportManager` for JSON-RPC serialization and response dispatching.

```csharp
public sealed class MyClientTransport(IClientTransportManager manager) : IClientTransport
{
    public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        // Connect to your underlying channel here.
        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        // Close your underlying channel here.
        return ValueTask.CompletedTask;
    }

    public async ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        // Serialize the JSON-RPC message to a string and send it to the underlying channel.
        var line = manager.WriteMessageAsync(message);
        await SendLineAsync(line, cancellationToken);
    }

    public ValueTask DisposeAsync() => DisconnectAsync();

    private async Task OnLineReceivedAsync(string line, CancellationToken cancellationToken)
    {
        // After receiving a server message on the underlying channel, deserialize and hand it back to the MCP client for processing.
        var message = await manager.ReadMessageAsync(line);
        switch (message)
        {
            case JsonRpcResponse response:
                await manager.HandleRespondAsync(response, cancellationToken);
                break;

            case JsonRpcRequest request:
                await manager.HandleServerRequestAsync(request, cancellationToken);
                break;
        }
    }

    private static ValueTask SendLineAsync(string line, CancellationToken cancellationToken)
    {
        // Write the line to your underlying channel.
        return ValueTask.CompletedTask;
    }
}
```

Register with the client:

```csharp
var mcpClient = new McpClientBuilder("Custom Transport Client", "1.0.0")
    .WithTransport(manager => new MyClientTransport(manager))
    .Build();
```

A server transport implements `IServerTransport`:

```csharp
public sealed class MyServerTransport(IServerTransportManager manager) : IServerTransport
{
    public Task<Task> StartAsync(CancellationToken startingCancellationToken, CancellationToken runningCancellationToken)
    {
        // The first Task completes when the transport has started; the second Task completes when the transport stops.
        return Task.FromResult(RunAsync(runningCancellationToken));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task OnLineReceivedAsync(string line, Stream responseStream, CancellationToken cancellationToken)
    {
        var message = await manager.ReadMessageAsync(line);
        if (message is not JsonRpcRequest request)
        {
            return;
        }

        // Hand the client request to the MCP server for processing.
        var response = await manager.HandleRequestAsync(request, cancellationToken: cancellationToken);
        if (response is not null)
        {
            await manager.WriteMessageAsync(responseStream, response, cancellationToken);
        }
    }

    private static async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
```

Register with the server:

```csharp
var mcpServer = new McpServerBuilder("Custom Transport Server", "1.0.0")
    .WithTransport(manager => new MyServerTransport(manager))
    .Build();
```

If your server transport needs to support the server proactively initiating requests to the client, such as Sampling, you also need to implement `IServerTransportSession` for each client connection.

> **Construction Principle**: The client transport constructor **only saves parameters and does not perform I/O**. All connection work (starting processes, establishing network connections, etc.) is done in `ConnectAsync`, and `ConnectAsync` must be idempotent. For details, see [Client "Build Before Connect" Principle](../knowledge/client-build-before-connect.md).
