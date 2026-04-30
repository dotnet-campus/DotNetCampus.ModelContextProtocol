# Sampling (Server-Initiated Requests)

Sampling is an MCP protocol feature that lets a server tool send a `sampling/createMessage` request to the client while executing, asking the client (the AI host) to call a language model and return the result. All transports (HTTP, stdio, In-Process, etc.) support this feature.

## Client: register a handler

Register a handler via `WithSamplingHandler` when building the client:

```csharp
var mcpClient = new McpClientBuilder()
    .WithLocalHostHttp(5943, "mcp")   // any transport works the same way
    .WithSamplingHandler(async (parameters, cancellationToken) =>
    {
        // Call your actual AI model here
        var response = await myAiModel.CompleteAsync(parameters.Messages);
        return new CreateMessageResult
        {
            Role = Role.Assistant,
            Content = new TextContentBlock { Text = response },
            Model = "my-model",
            StopReason = "endTurn",
        };
    })
    .Build();
```

## Server: initiate a request from a tool

Tool methods send the request via `IMcpServerCallToolContext.Sampling`:

```csharp
public class MyTool
{
    [McpServerTool]
    public async Task<string> AskAI(
        string question,
        IMcpServerCallToolContext context,
        CancellationToken cancellationToken)
    {
        var result = await context.Sampling.CreateMessageAsync(
            new CreateMessageRequestParams
            {
                Messages =
                [
                    new SamplingMessage
                    {
                        Role = Role.User,
                        Content = new TextContentBlock { Text = question },
                    },
                ],
                MaxTokens = 1024,
            },
            cancellationToken);

        return result.Content is TextContentBlock text ? text.Text : string.Empty;
    }
}
```

`IMcpServerCallToolContext` is an implicit parameter type — just declare it in the tool method's parameter list and the framework injects it automatically (see [Supported Types](QuickStart.md#supported-types)).
