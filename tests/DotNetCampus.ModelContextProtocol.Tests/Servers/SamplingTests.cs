using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Tests.McpTools;

namespace DotNetCampus.ModelContextProtocol.Tests.Servers;

/// <summary>
/// Sampling 功能集成测试：验证服务器向客户端发起 sampling/createMessage 请求的完整流程。
/// </summary>
[TestClass]
public class SamplingTests
{
    #region Sampling 基本功能

    [TestMethod("Sampling: 服务器工具可通过 context.Sampling 向客户端发起采样请求")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task ServerToolCanRequestSampling(HttpTransportType transportType)
    {
        // Arrange
        const string expectedResponseText = "Hello from LLM!";
        var samplingHandlerInvoked = false;

        await using var package = await TestMcpFactory.Shared.CreateHttpCoreAsync(
            transportType,
            configureBuilder: builder => builder.WithTools(t => t.WithTool(() => new SamplingTool())),
            configureClient: clientBuilder => clientBuilder.WithSamplingHandler(
                (parms, ct) =>
                {
                    samplingHandlerInvoked = true;
                    var result = new CreateMessageResult
                    {
                        Role = Role.Assistant,
                        Content = new TextContentBlock { Text = expectedResponseText },
                        Model = "test-model",
                        StopReason = "endTurn",
                    };
                    return Task.FromResult(result);
                }));

        // Act
        var toolArgs = JsonSerializer.SerializeToElement(new { message = "What's 2+2?" });
        var callResult = await package.Client.CallToolAsync("ask_llm", toolArgs);

        // Assert
        Assert.IsNotNull(callResult, "工具调用结果不应为 null");
        Assert.IsFalse(callResult.IsError, "工具调用不应返回错误");
        Assert.IsTrue(samplingHandlerInvoked, "客户端的 Sampling 处理器应被调用");

        var textContent = callResult.Content.OfType<TextContentBlock>().FirstOrDefault();
        Assert.IsNotNull(textContent, "工具调用结果应包含文本内容");
        Assert.AreEqual(expectedResponseText, textContent.Text, "工具返回的文本应与 Sampling 响应一致");
    }

    [TestMethod("Sampling: 无 Sampling 能力时 IsSupported 为 false")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task IsSupportedIsFalseWhenClientHasNoCapability(HttpTransportType transportType)
    {
        // Arrange - 客户端不配置 WithSamplingHandler，因此不声明采样能力
        await using var package = await TestMcpFactory.Shared.CreateHttpCoreAsync(
            transportType,
            configureBuilder: builder => builder.WithTools(t => t.WithTool(() => new SamplingTool())));

        // Act
        var toolArgs = JsonSerializer.SerializeToElement(new { });
        var callResult = await package.Client.CallToolAsync("check_sampling_capability");

        // Assert
        Assert.IsNotNull(callResult, "工具调用结果不应为 null");
        Assert.IsFalse(callResult.IsError, "工具调用不应返回错误");

        var textContent = callResult.Content.OfType<TextContentBlock>().FirstOrDefault();
        Assert.IsNotNull(textContent, "工具调用结果应包含文本内容");
        Assert.AreEqual("has_capability=False", textContent.Text,
            "when客户端未声明 Sampling 能力时，IsSupported 应为 false");
    }

    #endregion
}

