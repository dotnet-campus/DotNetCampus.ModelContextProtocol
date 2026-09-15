# Sampling

Sampling 允許伺服器端工具在執行期間向用戶端發起 `sampling/createMessage` 要求。用戶端決定是否呼叫模型、呼叫哪個模型，以及是否把結果傳回給伺服器端。

我們假設你在閱讀本文前，已經完成了 [快速開始](QuickStart.md) 中 MCP 伺服器和用戶端的建置。

## 伺服器端發起 Sampling 要求

Sampling 通常寫在工具方法中。工具方法透過 `IMcpServerCallToolContext.Sampling` 發起要求：

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

在初始化 MCP 伺服器時，將工具註冊進去即可：

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    .WithTools(t => t
        .WithTool(() => new SamplingTools())
    )
    .WithLocalHostHttp(5943, "mcp")
    .Build();
```

## 用戶端處理 Sampling 要求

用戶端透過 `WithSamplingHandler` 宣告自己支援 Sampling，並處理伺服器端發來的 `sampling/createMessage` 要求。

### 簡單多載

直接傳入處理函式：

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

### 工廠多載（相依性注入情節）

當處理函式依賴 `IServiceProvider` 中的服務時，可使用工廠多載：

```csharp
var mcpClient = new McpClientBuilder("示例客户端", "1.0.0")
    .WithServices(serviceProvider)  // 詳見[相依性注入](DependencyInjection.md)
    .WithHttp("http://localhost:5943/mcp")
    .WithSamplingHandler(services =>
    {
        var llmClient = services!.GetRequiredService<IMyLlmClient>();
        var userConsent = services.GetRequiredService<IUserConsentService>();

        return async (request, cancellationToken) =>
        {
            // 1. 请求用户确认
            if (!await userConsent.RequestSamplingConsentAsync(request, cancellationToken))
            {
                return CreateMessageResult.FromRefusal("用户拒绝了 Sampling 请求。");
            }

            // 2. 调用自己的大语言模型
            var prompt = request.Messages
                .Select(x => x.Content)
                .OfType<TextContentBlock>()
                .FirstOrDefault()
                ?.Text ?? string.Empty;
            var response = await llmClient.GenerateAsync(prompt, cancellationToken);

            // 3. 让用户确认是否允许返回结果
            if (!await userConsent.RequestResultConsentAsync(response, cancellationToken))
            {
                return CreateMessageResult.FromRefusal("用户拒绝返回 Sampling 结果。");
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

然後像呼叫一般工具一樣呼叫伺服器端工具：

```csharp
var arguments = JsonSerializer.SerializeToElement(new { question = "请用一句话介绍 MCP。" });
var result = await mcpClient.CallToolAsync("ask_llm", arguments);

Console.WriteLine(result.Content);
```

如果用戶端沒有呼叫 `WithSamplingHandler`，伺服器端工具中的 `context.Sampling.IsSupported` 會傳回 `false`。
