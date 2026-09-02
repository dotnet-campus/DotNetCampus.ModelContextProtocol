# Transport

The MCP transport layer is responsible only for sending and receiving JSON-RPC messages. Business code usually only needs to select the same transport layer on both the server and client sides.

## Overview

| Transport          | Built-in | Listen Address           | Use Case                          |
|--------------------|----------|--------------------------|-----------------------------------|
| HTTP (built-in)    | ✅       | `localhost` only         | Local development, single-machine |
| TouchSocket HTTP   | ❌ ext.  | `0.0.0.0` etc. supported | LAN/public network                |
| stdio              | ✅       | -                        | Client launches server process    |
| In-Process         | ✅       | -                        | Same-process embedding, testing   |
| IPC                | ❌ ext.  | -                        | Cross-process on same machine     |

> The core library's built-in HTTP transport only listens on the loopback address, for security and to minimize dependencies. If you need to listen on non-loopback addresses like `0.0.0.0`, use [TouchSocket HTTP](#touchsocket-http-extension). If you need the IPC transport, use [IPC](#ipc). See [Two Ways to Obtain Extended Transports](#two-ways-to-obtain-extended-transports) for how to get them.

---

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

> The built-in HTTP server transport (`LocalHostHttpServerTransport`) only listens on `127.0.0.1` and `[::1]`. If you need to listen on other addresses (such as `0.0.0.0`), see the [TouchSocket HTTP (Extension)](#touchsocket-http-extension) section below.

---

## TouchSocket HTTP (Extension)

The core library's built-in HTTP transport only listens on the local loopback address. If you need to listen on non-loopback addresses like `0.0.0.0` (e.g., when deploying to a LAN or the public internet), use the TouchSocket HTTP transport.

There are two ways to obtain the TouchSocket HTTP transport; see [Two Ways to Obtain Extended Transports](#two-ways-to-obtain-extended-transports) for details. The following examples assume you have obtained TouchSocket HTTP transport support through either method:

### Server

```csharp
// Simple overload: listen on localhost
var mcpServer = new McpServerBuilder("Example Server", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithTouchSocketHttp(5943, "mcp")
    .Build();

// Listen on 0.0.0.0 (all network interfaces, including LAN and public)
var mcpServer = new McpServerBuilder("Public Server", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithTouchSocketHttp(["0.0.0.0:5943", "[::]:5943"], "mcp")
    .Build();

// Options overload: configure all parameters
var mcpServer = new McpServerBuilder("Example Server", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithTouchSocketHttp(new TouchSocketHttpServerTransportOptions
    {
        Listen = ["0.0.0.0:5943", "[::]:5943"],
        EndPoint = "mcp",
    })
    .Build();
```

The `Listen` list uses the `"IP:port"` format. Only IP addresses are allowed — domain names cannot be used. Multiple addresses and ports can be listened on simultaneously.

### Reuse an Existing HttpService

If you already have a running `HttpService` (the core type of TouchSocket), you can attach the MCP server as a plugin:

```csharp
// httpService is your existing HttpService instance
httpService.UseMcpServer("Example Server", "1.0.0", builder =>
{
    builder.WithTools(t => t.WithTool(() => new SampleTools()));
});

// You can also specify a custom endpoint
httpService.UseMcpServer("Example Server", "1.0.0", "/custom-mcp", builder =>
{
    builder.WithTools(t => t.WithTool(() => new SampleTools()));
});
```

> `HttpService` implements the `IPluginManager` interface. `UseMcpServer` is an extension method on `IPluginManager`.

### Client

The TouchSocket HTTP transport client requires no special handling — the client simply makes HTTP requests to the server and can directly reuse the core library's HTTP client transport:

```csharp
var mcpClient = new McpClientBuilder("Example Client", "1.0.0")
    .WithHttp("http://192.168.1.100:5943/mcp")
    .Build();
```

---

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

The IPC transport layer is suitable for communication between different processes on the same machine, based on named pipes provided by dotnetCampus.Ipc.

There are two ways to obtain the IPC transport; see [Two Ways to Obtain Extended Transports](#two-ways-to-obtain-extended-transports) for details. The following examples assume you have obtained IPC transport support through either method:

### Server

```csharp
var mcpServer = new McpServerBuilder("IPC Example Server", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // sample-mcp-pipe is the local pipe name; clients must use the same name to connect
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();

await mcpServer.RunAsync();
```

You can also reuse an externally created `IpcProvider`:

```csharp
var mcpServer = new McpServerBuilder("IPC Example Server", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    .WithDotNetCampusIpc(existingIpcProvider)
    .Build();
```

### Client

```csharp
await using var mcpClient = new McpClientBuilder("IPC Example Client", "1.0.0")
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();
```

You can also reuse an externally created `IpcProvider`:

```csharp
await using var mcpClient = new McpClientBuilder("IPC Example Client", "1.0.0")
    .WithDotNetCampusIpc(existingIpcProvider, "sample-mcp-pipe")
    .Build();
```

---

## Two Ways to Obtain Extended Transports

The core library only includes three built-in transports: HTTP (localhost), stdio, and In-Process. If you need the TouchSocket HTTP or IPC transport, you can obtain them in one of two ways:

### Method 1: Install Extension Packages (Recommended)

Install the corresponding extension NuGet packages directly:

```bash
# TouchSocket HTTP transport
dotnet add package DotNetCampus.ModelContextProtocol.TouchSocket.Http

# IPC transport
dotnet add package DotNetCampus.ModelContextProtocol.Ipc
```

Once installed, you can directly use extension methods such as `.WithTouchSocketHttp()` / `.WithDotNetCampusIpc()`. The extension packages already pull in the underlying dependencies (`TouchSocket.Http` / `dotnetCampus.Ipc`) — this is the simplest approach.

### Method 2: Install Underlying Libraries + Enable Source Generator

If you want to minimize the number of `.dll` files pulled into your project (I just prefer to do it this way), you can skip the extension packages, install the underlying libraries directly, and enable the source generator to automatically generate the transport code:

```bash
# Install the underlying libraries (not the extension packages)
dotnet add package dotnetCampus.Ipc
dotnet add package TouchSocket.Http
```

Then enable the source generator in your project's `.csproj`:

```xml
<PropertyGroup>
  <DotNetCampusModelContextProtocolGenerateTransports>true</DotNetCampusModelContextProtocolGenerateTransports>
</PropertyGroup>
```

With this option enabled, the analyzer (Analyzer) bundled with the `DotNetCampus.ModelContextProtocol` core package will automatically scan the libraries installed in your project:

| Detected Library    | Auto-Generated Transport    |
|---------------------|-----------------------------|
| `dotnetCampus.Ipc`  | IPC transport               |
| `TouchSocket.Http`  | TouchSocket HTTP transport  |

**Libraries that are not installed will not generate any code** — the source generator is safe and will not pollute your project.

> **Design Philosophy**: The dotnet-campus organization favors keeping the core library zero-dependency and lightweight. IPC and TouchSocket HTTP are optional extended transports that are not forcibly bundled into the core library. Developers can pick and choose the transports they need without being forced to pull in unnecessary dependencies.

---

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
