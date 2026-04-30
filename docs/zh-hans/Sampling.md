# Sampling（服务端主动请求）

Sampling 是 MCP 协议的一项功能，允许服务端工具在执行期间向客户端发起 `sampling/createMessage` 请求，由客户端（AI 宿主）调用大语言模型并返回结果。所有传输层（HTTP、stdio、In-Process 等）均支持此功能。

## 客户端：注册处理器

在构建客户端时，通过 `WithSamplingHandler` 注册处理器：

```csharp
var mcpClient = new McpClientBuilder()
    .WithLocalHostHttp(5943, "mcp")   // 任意传输层均可
    .WithSamplingHandler(async (parameters, cancellationToken) =>
    {
        // 在这里调用实际的 AI 模型
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

## 服务端：在工具中发起请求

工具方法通过 `IMcpServerCallToolContext.Sampling` 发起请求：

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

`IMcpServerCallToolContext` 是隐式参数类型，直接声明在工具方法参数列表中即可，无需额外配置（参见[支持的类型](QuickStart.md#支持的类型)）。
