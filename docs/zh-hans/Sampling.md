# Sampling

Sampling 允许服务端工具在执行期间向客户端发起 `sampling/createMessage` 请求。客户端决定是否调用模型、调用哪个模型，以及是否把结果返回给服务端。

我们假设你在阅读本文前，已经完成了 [快速开始](QuickStart.md) 中 MCP 服务器和客户端的搭建。

## 服务端发起 Sampling 请求

Sampling 通常写在工具方法中。工具方法通过 `IMcpServerCallToolContext.Sampling` 发起请求：

```csharp
public class SamplingTools
{
    /// <summary>
    /// 通过客户端的大语言模型回答问题。
    /// </summary>
    /// <param name="context">当前工具调用上下文。</param>
    /// <param name="question">要发送给客户端模型的问题。</param>
    /// <returns>客户端模型返回的文本。</returns>
    [McpServerTool]
    public async Task<string> AskLlm(IMcpServerCallToolContext context, string question)
    {
        if (!context.Sampling.IsSupported)
        {
            return "当前客户端未声明 Sampling 能力。";
        }

        var result = await context.Sampling.CreateMessageAsync(
            question,
            maxTokens: 1024,
            cancellationToken: context.CancellationToken);

        return result.Content is TextContentBlock text ? text.Text : string.Empty;
    }
}
```

在初始化 MCP 服务器时，将工具注册进去即可：

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    .WithTools(t => t
        .WithTool(() => new SamplingTools())
    )
    .WithLocalHostHttp(5943, "mcp")
    .Build();
```

## 客户端处理 Sampling 请求

客户端通过 `WithSamplingHandler` 声明自己支持 Sampling，并处理服务端发来的 `sampling/createMessage` 请求：

```csharp
var mcpClient = new McpClientBuilder("示例客户端", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .WithSamplingHandler(async (request, cancellationToken) =>
    {
        // 真实应用中通常需要在这里：
        // 1. 让用户确认是否允许本次 Sampling；
        // 2. 根据 request.Messages 调用自己的大语言模型；
        // 3. 让用户确认是否允许把结果返回给服务端。
        var prompt = request.Messages
            .Select(x => x.Content)
            .OfType<TextContentBlock>()
            .FirstOrDefault()
            ?.Text ?? string.Empty;

        await Task.Yield();
        return new CreateMessageResult
        {
            Role = Role.Assistant,
            Content = new TextContentBlock { Text = $"客户端模型收到：{prompt}" },
            Model = "demo-model",
            StopReason = "endTurn",
        };
    })
    .Build();
```

然后像调用普通工具一样调用服务端工具：

```csharp
var arguments = JsonSerializer.SerializeToElement(new { question = "请用一句话介绍 MCP。" });
var result = await mcpClient.CallToolAsync("ask_llm", arguments);

Console.WriteLine(result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text);
```

如果客户端没有调用 `WithSamplingHandler`，服务端工具中的 `context.Sampling.IsSupported` 会返回 `false`。
