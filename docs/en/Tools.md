# Tools

Tools allow clients to request the server to perform actions. We assume you have already completed the MCP server and client setup described in [Quick Start](QuickStart.md) before reading this guide.

## Server-Side Tool Implementation

### Basic Example

A simple MCP tool implementation looks like this:

```csharp
public class SampleTools
{
    /// <summary>
    /// A debugging tool for AI that echoes back some information as-is.
    /// </summary>
    /// <param name="text">The string to echo back.</param>
    /// <returns>The echoed string.</returns>
    [McpServerTool(ReadOnly = true)]
    public string EchoTool(string text)
    {
        return text;
    }
}
```

In this example:

- XML comments become an important part of the tool's description and are sent to the client via the MCP protocol, so writing good comments is critical for the LLM to correctly use the tool. Also, you don't need to worry about multi-language comments — LLMs don't care which language you use to describe the tool.
- If parameters are complex data types or enums, you don't need to describe every internal field or property in detail within the parameter comments, because this library automatically extracts comments recursively and includes them in the MCP protocol when sending to the client.
- Tool names use snake_case by default, derived from the method name. In this example, you would get `echo_tool`.

### Custom Tool Attributes

The `[McpServerTool]` attribute supports several properties that give you fine-grained control over the tool's behavior and metadata in the MCP protocol:

```csharp
/// <summary>
/// A debugging tool for AI that echoes back some information as-is.
/// </summary>
/// <param name="text">The string to echo back.</param>
[McpServerTool(
    Name = "echo_tool",          // Override the tool name to decouple from the method name (e.g. to avoid Async suffix affecting the tool name)
    Title = "Echo Output",       // Human-readable tool title, invisible to AI; can be used for UI display
    Description = "A debugging tool for AI that echoes back some information as-is.",  // Override the description from method XML comments
    Idempotent = true,           // Mark as idempotent; clients can safely retry calls
    OpenWorld = false,           // Mark that this tool does not interact with the external open world
    ReadOnly = true              // Mark as read-only; calling it does not modify its environment
)]
public string EchoCustomized(string text)
{
    return text;
}
```

Property descriptions:

- **Name**: The tool name in the MCP protocol. Uses the method's snake_case name if not specified.
- **Title**: A human-readable tool title, invisible to AI; can be used for UI display.
- **Description**: The tool description, overriding the description in the method's XML comments.
- **Idempotent**: Whether the tool is idempotent. Idempotent tools can be safely retried by the client on network errors.
- **OpenWorld**: Whether the tool interacts with the external open world (e.g. calling a web API).
- **ReadOnly**: Whether the tool is read-only. Read-only tools do not modify their environment when called.

### Parameters and Context

#### Implicit Parameter Types

Tool methods can receive the following implicit parameters — just add them to the method signature as needed:

- Any JSON-deserializable type (primitives, arrays, objects, etc.) — passed in by the MCP client
- `CancellationToken` — triggered when the client cancels the tool call; recommended to always declare as the last parameter
- `IMcpServerCallToolContext` — provides contextual information about the current tool call
- `JsonElement` — receives arbitrary JSON data; suitable for scenarios where the parameter structure is uncertain

#### Explicit Parameter Types (`[ToolParameter]` Attribute)

Use the `[ToolParameter]` attribute to mark special parameter behavior:

- `[ToolParameter(Type = ToolParameterType.InputObject)]`: This parameter receives the entire input object of the tool call (deserialized into a type). After using this attribute, **no other** plain parameters are allowed.
- `[ToolParameter(Type = ToolParameterType.Injected)]`: This parameter is automatically injected by the dependency injection framework, not passed through the MCP protocol layer. Requires `IServiceProvider` to be configured during server initialization.

#### IMcpServerCallToolContext

`IMcpServerCallToolContext` provides contextual information during tool method execution, including:

- Current tool name (`context.Name`)
- Raw JSON input arguments (`context.InputJsonArguments`)
- `_meta` metadata from the request (`context.Meta`), useful for distributed tracing
- MCP server information (`context.McpServer.ServerName`)
- HTTP transport layer context (`context.HttpTransportContext`, including SessionId, Headers, etc.)

> **Important**: The `IMcpServerCallToolContext` instance is **only valid during the current tool method execution**. Do not store it in static fields or pass it across async boundaries, as the context becomes invalid once the tool call completes.

#### Full Parameter Example

The following example demonstrates combined usage of `IMcpServerCallToolContext`, parameters with default values, and nullable parameters:

```csharp
/// <summary>
/// A debugging tool for AI that echoes back some information as-is.
/// </summary>
/// <param name="text">The string to echo back.</param>
/// <param name="options">How to return the string.</param>
/// <param name="count">The number of times to return the string.</param>
/// <param name="extraData">Meaningless extra data.</param>
[McpServerTool(Name = "echo_tool")]
public Task<EchoResult> EchoAsync(
    IMcpServerCallToolContext context,
    string text,
    EchoOptions options = EchoOptions.JsonObject,
    int count = 1,
    EchoExtraData? extraData = null)
{
    var info = $"""
        Server name: {context.McpServer.ServerName}
        SessionId: {context.HttpTransportContext?.SessionId}
        Headers: {string.Join(", ", context.HttpTransportContext?.Headers)}
        InputJsonArguments: {context.InputJsonArguments}
        """;
    var result = $"""
        Echoing text: {text}
        Options: {options}
        Count: {count}
        ExtraData: {extraData}
        """;
    return Task.FromResult(new EchoResult { Info = info, Result = result });
}
```

(The definitions of `EchoOptions`, `EchoExtraData`, and `EchoResult` used in this example can be found in [Auxiliary Types](#auxiliary-types) below.)

### Return Value Types

The method return value can be one of the following types:

- `string`: A string returned to the AI (typically natural language understandable by AI)
- `void`: No return value. **Note**: Although this is a type supported by the MCP protocol, some MCP clients may error when the server returns an empty result. In such cases, consider returning a `string` with an empty value instead.
- Any JSON-serializable type (per MCP protocol specification, the **return value must be an object type**, not an array or primitive)
- `CallToolResult`: A generic tool call result — the final data structure of the MCP protocol layer. Using this return type allows you to directly control the data returned to the AI at the protocol level.
- `CallToolResult<T>`: A tool call result with a structured data type, created via `CallToolResult<T>.FromResult(result)`. `T` is any JSON-serializable type. Using this return type gives you structured return value capabilities while retaining protocol-level control over the returned data.

**Notably**, when the return value is a JSON-serializable object, per the MCP protocol specification we return structured data and also include the JSON-serialized string in the plain text return value (for compatibility). The tool will also be marked as "having structured return values".

### Synchronous vs. Asynchronous

Methods can be synchronous or asynchronous:

- Synchronous: Supports all of the above return value types
- Asynchronous: Supports `Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>` async return types

### How Tools Report Errors

Tools can report errors to the client in two ways. Choose based on your scenario:

#### Method 1: Throw `McpToolUsageException`

Suitable for "the user used this tool incorrectly" scenarios, such as invalid parameters. Throwing this automatically returns `isError: true` to the client through the MCP protocol layer — no need to change the return value type:

```csharp
[McpServerTool]
public string Echo(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        throw new McpToolUsageException("The text parameter cannot be empty.");
    }
    return text;
}
```

#### Method 2: Return `CallToolResult.FromError()`

Suitable for scenarios requiring precise control over the returned content or structured error information:

```csharp
[McpServerTool]
public CallToolResult SafeEcho(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return CallToolResult.FromError("The text parameter cannot be empty.");
    }
    return text;
}
```

Differences between the two approaches:

| | `McpToolUsageException` | `CallToolResult.FromError()` |
|---|---|---|
| Return value type | Any type (no signature change) | Must be `CallToolResult` |
| Use case | Fail fast, no further execution | Structured error info or precise control over return format |
| Advantage | Simple and direct, minimal code changes | Maximum flexibility |

### Auxiliary Types

The following are the auxiliary type definitions used in the complex examples above:

```csharp
/// <summary>
/// How to return the string.
/// </summary>
public enum EchoOptions
{
    /// <summary>
    /// Return as plain text.
    /// </summary>
    PlainText,

    /// <summary>
    /// Return as a JSON object.
    /// </summary>
    JsonObject,
}

/// <summary>
/// Meaningless extra data.
/// </summary>
/// <param name="Data1">The first storable value.</param>
public record EchoExtraData(string Data1)
{
    /// <summary>
    /// The second storable value.
    /// </summary>
    public string Data2 { get; init; } = "";
}

/// <summary>
/// Tool return value for AI debugging use.
/// </summary>
public record EchoResult
{
    /// <summary>
    /// Context information for AI debugging use.
    /// </summary>
    public string Info { get; init; } = "";

    /// <summary>
    /// Result information for AI debugging use.
    /// </summary>
    public string Result { get; init; } = "";
}
```

## Server-Side Advanced Initialization

### JSON Serialization and Dependency Injection

When your tool parameters or return values use custom types, you need to provide a JSON serialization context to support AOT compilation. If you want your tool classes to support dependency injection, provide an `IServiceProvider` instance:

```csharp
var mcpServer = new McpServerBuilder("Example Server", "1.0.0")
    // Provide JSON serialization context (AOT-compatible)
    .WithJsonSerializer(McpToolJsonContext.Default)

    // Provide IServiceProvider to support constructor injection in tool classes
    // and [ToolParameter(Type = ToolParameterType.Injected)] injection in tool method parameters
    .WithServices(appServiceProvider)

    .WithTools(t => t
        // Plain registration: new instance created per call
        .WithTool(() => new SampleTools())
        // DI registration: lifecycle managed by IServiceProvider (requires WithServices to be configured)
        .WithTool<SampleTools2>()
    )

    .WithLocalHostHttp(5943, "mcp")
    .Build();
```

### Logging Integration

Bridge the MCP server's internal logs to your own logging system to monitor the server's health:

```csharp
var mcpServer = new McpServerBuilder("Example Server", "1.0.0")
    // The second parameter controls the log detail level for raw transport-layer messages; default is no logging
    .WithLogger(new McpLoggerBridge(myLogger), McpTransportRawMessageLoggingDetailLevel.Trimmed)
    // ... other configuration
    .Build();
```

Logger bridge implementation reference:

```csharp
internal class McpLoggerBridge(ILogger logger) : IMcpLogger
{
    public bool IsEnabled(LoggingLevel loggingLevel)
    {
        return logger.IsEnabled(loggingLevel.ToLogLevel());
    }

    public void Log<TState>(LoggingLevel loggingLevel, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        logger.Log(loggingLevel.ToLogLevel(), default, state, exception, formatter);
    }
}
```

### Request Interception

By inheriting from `McpServerRequestHandlers` and overriding methods, you can intercept all requests sent to this MCP server for unified processing:

```csharp
var mcpServer = new McpServerBuilder("Example Server", "1.0.0")
    .WithRequestHandlers(s => new CustomRequestHandlers(s))
    // ... other configuration
    .Build();
```

Interceptor implementation reference:

```csharp
internal class CustomRequestHandlers(McpServer server) : McpServerRequestHandlers(server)
{
    public override async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> rawRequest,
        string? toolName, IMcpServerTool? tool, IMcpServerCallToolContext? context)
    {
        var result = await base.CallToolAsync(rawRequest, toolName, tool, context);
        if (result.RawException is { } exception)
        {
            // Perform additional logging or alerting when a tool call throws an exception
            Log.Error("Tool call exception", exception);
        }
        return result;
    }
}
```

### Transport Layer Selection

This library supports multiple transport layers. Choose based on your deployment scenario:

| Transport | Method | Use Case |
|--------|------|---------|
| Streamable HTTP (built-in) | `.WithLocalHostHttp()` | Local communication, lightweight, zero dependencies |
| Streamable HTTP (TouchSocket) | `.WithTouchSocketHttp()` | Public network listening, high-performance HTTP |
| stdio | `.WithStdio()` | Standard input/output, officially recommended by MCP |
| dotnetCampus.Ipc | `.WithDotNetCampusIpc()` | Local high-performance IPC |

For detailed instructions and configuration, see [Choosing a Transport Layer](Transport.md).

## Client-Side Tool Invocation

### Client Builder Overloads

`McpClientBuilder`'s `WithHttp` and `WithStdio` methods each have two overloads: the simple overload is suitable for quick experimentation, while the options overload is suitable for production scenarios requiring custom configuration.

**HTTP transport:**

```csharp
// Simple overload: specify URL only
var client = new McpClientBuilder("Example Client", "1.0.0")
    .WithHttp("http://localhost:3001/mcp")
    .Build();

// Options overload: configure custom HttpClient, timeouts, etc.
var client = new McpClientBuilder("Example Client", "1.0.0")
    .WithHttp(new HttpClientTransportOptions
    {
        ServerUrl = "http://localhost:3001/mcp",
        HttpClient = customHttpClient,  // Can inject an HttpClient with authentication headers
    })
    .Build();
```

**stdio transport:**

```csharp
// Simple overload: specify command and arguments
var client = new McpClientBuilder("Example Client", "1.0.0")
    .WithStdio("npx", ["-y", "@modelcontextprotocol/server-everything", "stdio"])
    .Build();

// Options overload: configure environment variables
var client = new McpClientBuilder("Example Client", "1.0.0")
    .WithStdio(new StdioClientTransportOptions
    {
        Command = "python",
        Arguments = ["-m", "my_mcp_server"],
        EnvironmentVariables = new Dictionary<string, string>
        {
            ["PYTHONPATH"] = "/path/to/modules",
        },
    })
    .Build();
```

### Basic Invocation

A typical invocation flow for a single MCP client:

```csharp
var client = new McpClientBuilder("Example Client", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

// Optional: connect early to catch exceptions uniformly
await client.EnsureConnectedAsync();

// List tools
var tools = await client.ListToolsAsync();
foreach (var tool in tools.Tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

// Call a tool
var arguments = JsonSerializer.SerializeToElement(new { text = "Hello" });
var result = await client.CallToolAsync("echo_tool", arguments);
Console.WriteLine(result.Content);
```

### Multi-Server Management (Agent Scenarios)

In agent programs, you typically need to manage multiple MCP servers simultaneously (built-in tools, external services, position programs, etc.). For a complete MCP server manager example, see [McpServerManager](McpServerManager.md).
