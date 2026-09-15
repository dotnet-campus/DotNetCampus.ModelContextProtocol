using System.Runtime.CompilerServices;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Tests;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DotNetCampus.ModelContextProtocol.ClientExtensionsAIConnection.Tests;

[TestClass]
public sealed class ModelContextProtocolToolToAIToolExtensionTest
{
    [TestMethod]
    public async Task ListAIToolsAsync_And_InvokeAsync_ShouldCreateAndCallTool()
    {
        await using var package = await TestMcpFactory.Shared.CreateInProcessCoreAsync(builder =>
            builder.WithTools(tools => tools.WithTool(() => new AdditionTool())));

        IReadOnlyList<AITool> aiTools = await package.Client.ListAIToolsAsync();

        Assert.AreEqual(1, aiTools.Count);
        AITool aiTool = aiTools[0];
        Assert.AreEqual("add_numbers", aiTool.Name);

        var aiFunction = Assert.IsInstanceOfType<ModelContextProtocolAIFunction>(aiTool);
        object? result = await aiFunction.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
        {
            ["left"] = 10,
            ["right"] = 20,
        }));

        Assert.AreEqual("30", result as string);
    }

    [TestMethod]
    public async Task AgentFramework_ShouldInvokeToolFromFunctionCall()
    {
        await using var package = await TestMcpFactory.Shared.CreateInProcessCoreAsync(builder =>
            builder.WithTools(tools => tools.WithTool(() => new AdditionTool())));

        IReadOnlyList<AITool> aiTools = await package.Client.ListAIToolsAsync();
        var chatClient = new FakeFunctionCallChatClient("add_numbers", new Dictionary<string, object>
        {
            ["left"] = 7,
            ["right"] = 5,
        });

        var agent = chatClient.AsAIAgent(
            "TestAgent",
            "Use tools when needed.",
            "Tool invocation test agent.",
            [.. aiTools],
            null,
            null);

        var result = await agent.RunAsync([new ChatMessage(ChatRole.User, "请调用加法工具")]);
        ChatMessage lastMessage = result.Messages.Last();

        Assert.AreEqual(ChatRole.Assistant, lastMessage.Role);
        Assert.IsTrue(lastMessage.Contents.OfType<TextContent>().Any(content => content.Text == "工具结果: 12"));
        Assert.AreEqual(2, chatClient.CallCount);
    }
}

public class AdditionTool
{
    /// <summary>
    /// 计算两个整数之和。
    /// </summary>
    /// <param name="left">左操作数。</param>
    /// <param name="right">右操作数。</param>
    /// <returns>求和结果。</returns>
    [McpServerTool(ReadOnly = true)]
    public int AddNumbers(int left, int right)
    {
        return left + right;
    }
}

file sealed class FakeFunctionCallChatClient : IChatClient
{
    private readonly string _toolName;
    private readonly IDictionary<string, object> _arguments;

    public FakeFunctionCallChatClient(string toolName, IDictionary<string, object> arguments)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        _toolName = toolName;
        _arguments = arguments;
    }

    public int CallCount { get; private set; }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        CallCount++;
        ChatMessage responseMessage = CallCount == 1
            ? new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent("call-1", _toolName, _arguments),
            ])
            : new ChatMessage(ChatRole.Assistant,
            [
                new TextContent($"工具结果: {GetToolResult(messages)}"),
            ]);

        return Task.FromResult(new ChatResponse(responseMessage));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate
        {
            Role = response.Messages[^1].Role,
            Contents = [.. response.Messages[^1].Contents],
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    public void Dispose()
    {
    }

    private static object? GetToolResult(IEnumerable<ChatMessage> messages)
    {
        return messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Last()
            .Result;
    }
}
