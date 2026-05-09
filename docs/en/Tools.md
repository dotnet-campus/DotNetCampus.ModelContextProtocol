# Tools

Tools let a client ask a server to perform actions. See [Server Quick Start](Server_QuickStart.md) for the smallest server. This page shows a fuller tool shape.

## Implementing tools on the server

```csharp
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

var mcpServer = new McpServerBuilder("ToolsServer", "1.0.0")
    // Pass a JSON source generation context when tool inputs or outputs use custom types.
    .WithJsonSerializer(ToolJsonContext.Default)
    .WithTools(tools => tools.WithTool(() => new WeatherTools()))
    .WithLocalHostHttp(5943, "mcp")
    .Build();

#if DEBUG
mcpServer.EnableDebugMode();
#endif

await mcpServer.RunAsync();

public class WeatherTools
{
    [McpServerTool(
        Name = "get_weather",
        Title = "Get weather",
        Description = "Query weather for a city",
        ReadOnly = true,
        OpenWorld = true)]
    public async Task<WeatherResult> GetWeather(SearchRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken);

        return new WeatherResult
        {
            City = request.City,
            Temperature = 24,
            Unit = request.Unit,
            Summary = "Sunny"
        };
    }

    [McpServerTool(Name = "validate_city", ReadOnly = true)]
    public CallToolResult ValidateCity(string city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return CallToolResult.FromError("city cannot be empty.");
        }

        return $"city={city}";
    }

    [McpServerTool(Name = "inspect_request", ReadOnly = true)]
    public string InspectRequest(
        IMcpServerCallToolContext context,
        [ToolParameter(Type = ToolParameterType.InputObject)] SearchRequest request)
    {
        return $"tool={context.Name}, city={request.City}, raw={context.InputJsonArguments}";
    }
}

public record SearchRequest
{
    public required string City { get; init; }

    public string Unit { get; init; } = "celsius";
}

public record WeatherResult
{
    public required string City { get; init; }

    public required int Temperature { get; init; }

    public required string Unit { get; init; }

    public required string Summary { get; init; }
}

[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(WeatherResult))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    AllowOutOfOrderMetadataProperties = true)]
internal partial class ToolJsonContext : JsonSerializerContext;
```

Common implicit parameters:

- `CancellationToken`: triggered when the client cancels the tool call.
- `IMcpServerCallToolContext`: reads tool name, raw input, services, Sampling, and other context.
- `JsonElement` or `object`: receives arbitrary JSON data.

Common return values:

- `string`: returns text.
- Custom object: returns structured content and requires a JSON source generation context.
- `CallToolResult`: controls the MCP result directly, such as returning `isError=true`.
- `Task<T>` or `ValueTask<T>`: asynchronous tools.

## Calling tools from the client

```csharp
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

await using var mcpClient = new McpClientBuilder()
    .WithClientInfo("ToolsClient", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools.Tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

var arguments = JsonSerializer.SerializeToElement(new
{
    city = "Hangzhou",
    unit = "celsius"
});

var result = await mcpClient.CallToolAsync("get_weather", arguments);

Console.WriteLine(result.StructuredContent?.GetRawText());
Console.WriteLine(result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text);
```
