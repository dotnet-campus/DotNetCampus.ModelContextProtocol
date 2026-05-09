# Sampling

Sampling lets a server tool send a `sampling/createMessage` request to the client while the tool is running. The client decides whether to call a model, which model to call, and whether to return the result to the server.

## Complete example

```csharp
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

var mcpServer = new McpServerBuilder("SamplingServer", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SamplingTools()))
    .WithInProcess()
    .Build();

await mcpServer.StartAsync();
try
{
    await using var mcpClient = new McpClientBuilder()
        .WithClientInfo("SamplingClient", "1.0.0")
        .WithInProcess(mcpServer)
        .WithSamplingHandler((request, cancellationToken) =>
        {
            var prompt = request.Messages
                .Select(message => message.Content)
                .OfType<TextContentBlock>()
                .FirstOrDefault()
                ?.Text ?? string.Empty;

            return Task.FromResult(new CreateMessageResult
            {
                Role = Role.Assistant,
                Content = new TextContentBlock { Text = $"Client model received: {prompt}" },
                Model = "demo-model",
                StopReason = "endTurn",
            });
        })
        .Build();

    var arguments = JsonSerializer.SerializeToElement(new { question = "Hello, MCP." });
    var result = await mcpClient.CallToolAsync("ask_model", arguments);

    Console.WriteLine(result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text);
}
finally
{
    await mcpServer.StopAsync();
}

public class SamplingTools
{
    [McpServerTool]
    public async Task<string> AskModel(IMcpServerCallToolContext context, string question)
    {
        if (!context.Sampling.IsSupported)
        {
            return "The current client did not declare Sampling support.";
        }

        var result = await context.Sampling.CreateMessageAsync(
            question,
            maxTokens: 256,
            cancellationToken: context.CancellationToken);

        return result.Content is TextContentBlock text ? text.Text : string.Empty;
    }
}
```

## Handling Sampling requests

The client declares and handles Sampling requests with `WithSamplingHandler`. A server tool starts a request through `IMcpServerCallToolContext.Sampling`; check `IsSupported` before calling it.
