# Sampling

Sampling allows server-side tools to send `sampling/createMessage` requests to the client during execution. The client decides whether to call a model, which model to call, and whether to return the result to the server.

We assume you have already completed the MCP server and client setup described in [Quick Start](QuickStart.md) before reading this guide.

## Server-Side Initiation of Sampling Requests

Sampling is typically written within tool methods. The tool method initiates requests through `IMcpServerCallToolContext.Sampling`:

```csharp
public class SamplingTools
{
    /// <summary>
    /// Answers a question using the client's large language model.
    /// </summary>
    /// <param name="context">The current tool call context.</param>
    /// <param name="question">The question to send to the client-side model.</param>
    /// <returns>The text returned by the client-side model.</returns>
    [McpServerTool]
    public async Task<string> AskLlm(IMcpServerCallToolContext context, string question)
    {
        if (!context.Sampling.IsSupported)
        {
            return "The current client has not declared Sampling capability.";
        }

        var result = await context.Sampling.CreateMessageAsync(
            question,
            maxTokens: 1024,
            cancellationToken: context.CancellationToken);

        return result.Content is TextContentBlock text ? text.Text : string.Empty;
    }
}
```

Simply register the tool when initializing the MCP server:

```csharp
var mcpServer = new McpServerBuilder("Example Server", "1.0.0")
    .WithTools(t => t
        .WithTool(() => new SamplingTools())
    )
    .WithLocalHostHttp(5943, "mcp")
    .Build();
```

## Client-Side Handling of Sampling Requests

The client declares its Sampling support via `WithSamplingHandler` and handles `sampling/createMessage` requests sent by the server.

### Simple Overload

Pass the handler function directly:

```csharp
var mcpClient = new McpClientBuilder("Example Client", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .WithSamplingHandler(async (request, cancellationToken) =>
    {
        // In a real application, you typically need to:
        // 1. Let the user confirm whether to allow this Sampling request;
        // 2. Call your own large language model based on request.Messages;
        // 3. Let the user confirm whether to allow returning the result to the server.
        var prompt = request.Messages
            .Select(x => x.Content)
            .OfType<TextContentBlock>()
            .FirstOrDefault()
            ?.Text ?? string.Empty;

        await Task.Yield();
        return new CreateMessageResult
        {
            Role = Role.Assistant,
            Content = new TextContentBlock { Text = $"Client model received: {prompt}" },
            Model = "demo-model",
            StopReason = "endTurn",
        };
    })
    .Build();
```

### Factory Overload (Dependency Injection Scenarios)

When the handler function depends on services from `IServiceProvider`, use the factory overload:

```csharp
var mcpClient = new McpClientBuilder("Example Client", "1.0.0")
    .WithServices(serviceProvider)  // See [Dependency Injection](DependencyInjection.md)
    .WithHttp("http://localhost:5943/mcp")
    .WithSamplingHandler(services =>
    {
        var llmClient = services!.GetRequiredService<IMyLlmClient>();
        var userConsent = services.GetRequiredService<IUserConsentService>();

        return async (request, cancellationToken) =>
        {
            // 1. Request user consent
            if (!await userConsent.RequestSamplingConsentAsync(request, cancellationToken))
            {
                return CreateMessageResult.FromRefusal("User rejected the Sampling request.");
            }

            // 2. Call your own large language model
            var prompt = request.Messages
                .Select(x => x.Content)
                .OfType<TextContentBlock>()
                .FirstOrDefault()
                ?.Text ?? string.Empty;
            var response = await llmClient.GenerateAsync(prompt, cancellationToken);

            // 3. Let the user confirm whether to allow returning the result
            if (!await userConsent.RequestResultConsentAsync(response, cancellationToken))
            {
                return CreateMessageResult.FromRefusal("User rejected returning the Sampling result.");
            }

            return new CreateMessageResult
            {
                Role = Role.Assistant,
                Content = new TextContentBlock { Text = response },
                Model = response.Model,
                StopReason = "endTurn",
            };
        };
    })
    .Build();
```

Then call server-side tools just like normal tools:

```csharp
var arguments = JsonSerializer.SerializeToElement(new { question = "Please introduce MCP in one sentence." });
var result = await mcpClient.CallToolAsync("ask_llm", arguments);

Console.WriteLine(result.Content);
```

If the client has not called `WithSamplingHandler`, `context.Sampling.IsSupported` in the server-side tool will return `false`.
