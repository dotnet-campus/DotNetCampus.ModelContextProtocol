# In-Process Transport Guide

## Usage

```csharp
var mcpServer = new McpServerBuilder("Embedded Server", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    // WithInProcess creates the channel pair and returns it via the out parameter
    .WithInProcess(out var transportPair)
    .Build();

await mcpServer.StartAsync();

var mcpClient = new McpClientBuilder()
    .WithInProcess(transportPair)
    .Build();

var tools = await mcpClient.ListToolsAsync();
```

Each `InProcessTransportPair` is a one-to-one connection — one pair can only be bound to one server and one client. For multiple concurrent clients, call `WithInProcess` multiple times on the server:

```csharp
var mcpServer = new McpServerBuilder("Embedded Server", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithInProcess(out var pair1)
    .WithInProcess(out var pair2)
    .Build();

await mcpServer.StartAsync();

var client1 = new McpClientBuilder().WithInProcess(pair1).Build();
var client2 = new McpClientBuilder().WithInProcess(pair2).Build();
```

The In-Process transport fully supports Sampling. See the [Sampling Guide](Sampling.md) for details.

Custom types used as tool parameters or return values must still be registered with `WithJsonSerializer` (the In-Process transport still uses JSON-RPC text format and does not bypass serialization):

```csharp
var mcpServer = new McpServerBuilder("Embedded Server", "1.0.0")
    .WithJsonSerializer(MyToolJsonContext.Default)   // required for custom types
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithInProcess(out var transportPair)
    .Build();
```

The In-Process transport provides no process isolation. The server and client run in the same process under the same privilege level, so it is only suitable for trusted embedded or testing scenarios.
