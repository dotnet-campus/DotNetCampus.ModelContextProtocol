# Quick Start

## Initialization

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

## Declaring MCP Tool Methods

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

### Supported Types

Method parameters can be of any number and support the following types:

- Implicit types:
    - Any type that can be JSON deserialized (including primitive types, arrays, objects, etc.)
    - `CancellationToken`: Represents a cancellation token
    - `IMcpServerCallToolContext`: Represents the context information of the current tool method
    - `JsonElement`: Represents arbitrary JSON data
- Explicit types:
    - `[ToolParameter(Type = ToolParameterType.InputObject)]`: Indicates this parameter receives the entire input object of the tool call; no other regular parameters are allowed when this is used
    - `[ToolParameter(Type = ToolParameterType.Injected)]`: Indicates this parameter is automatically injected by the dependency injection framework, not passed through the MCP protocol layer

Method return values can be of the following types:

- `string`: Represents a string returned to the AI (typically natural language that can be understood by the AI)
- `void`: Indicates no return value **Note that while this is supported by the MCP protocol, some MCP clients may throw exceptions when the MCP server returns an empty result; in such cases, it's recommended to use `string` as the return type and return an empty string**
- Any type that can be JSON serialized (according to the MCP protocol specification, **return values must be object types**, not arrays or primitive types)
- `CallToolResult`: A generic tool call result, which is the final data structure at the MCP protocol layer; using this return type, you can directly control the data returned to the AI at the MCP protocol layer
- `CallToolResult<T>`: A tool call result with a structured data type, created via the `CallToolResult.FromResult(result)` method, where `T` is any type that can be JSON serialized; using this return type, you can control the data returned to the AI at the MCP protocol layer while still maintaining structured return value functionality

**Notably**, when the return value is a JSON-serializable object, according to the MCP protocol specification, we return structured data and also include the JSON serialized string of this data in the plain string return value (for compatibility). Additionally, this tool will be marked as "having structured return values".

Methods can be synchronous or asynchronous:

- Supports all the above synchronous return value types
- Supports `Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>` asynchronous return values

### Type Polymorphism

Method parameters and return values can use interface or abstract class types, but all possible concrete implementation types must be annotated:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Foo), typeDiscriminator: "foo")]
[JsonDerivedType(typeof(Bar), typeDiscriminator: "bar")]
public interface IFooBar
{
    [JsonPropertyName("name")]
    string? Name { get; init; }
}

public class Foo : IFooBar
{
    public string? Name { get; init; }

    [JsonPropertyName("fooValue")]
    public int FooValue { get; init; }
}

public class Bar : IFooBar
{
    public string? Name { get; init; }

    [JsonPropertyName("barValue")]
    public string? BarValue { get; init; }
}
```

Please note that the Json serializer must be annotated with `AllowOutOfOrderMetadataProperties`, as AI may not always pass parameters in order:

```csharp
[JsonSerializable(typeof(IFooBar))]
[JsonSourceGenerationOptions(
    AllowOutOfOrderMetadataProperties = true)]
internal partial class McpToolJsonContext : JsonSerializerContext;
```
