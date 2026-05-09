# Sampling

Sampling 允许服务端工具在执行期间向客户端发起 `sampling/createMessage` 请求。客户端决定是否调用模型、调用哪个模型，以及是否把结果返回给服务端。

## 完整示例

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
                Content = new TextContentBlock { Text = $"客户端模型收到：{prompt}" },
                Model = "demo-model",
                StopReason = "endTurn",
            });
        })
        .Build();

    var arguments = JsonSerializer.SerializeToElement(new { question = "你好，MCP。" });
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
            return "当前客户端未声明 Sampling 能力。";
        }

        var result = await context.Sampling.CreateMessageAsync(
            question,
            maxTokens: 256,
            cancellationToken: context.CancellationToken);

        return result.Content is TextContentBlock text ? text.Text : string.Empty;
    }
}
```

## 使用位置

客户端通过 `WithSamplingHandler` 声明并处理 Sampling 请求。服务端工具通过 `IMcpServerCallToolContext.Sampling` 发起请求，调用前先检查 `IsSupported`。
