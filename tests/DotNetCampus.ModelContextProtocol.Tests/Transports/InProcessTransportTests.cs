using System.Text.Json;
using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Servers;
using DotNetCampus.ModelContextProtocol.Tests.McpTools;
using DotNetCampus.ModelContextProtocol.Transports.InProcess;

namespace DotNetCampus.ModelContextProtocol.Tests.Transports;

/// <summary>
/// In-Process 传输层测试。
/// </summary>
[TestClass]
public class InProcessTransportTests
{
    [TestMethod("InProcess Connect: 连接成功并能调用工具")]
    public async Task Connect()
    {
        await using var package = await TestMcpFactory.Shared.CreateSimpleInProcessAsync();

        var result = await package.Client.ListToolsAsync();

        Assert.IsTrue(package.Client.IsConnected);
        Assert.AreEqual(1, result.Tools.Count);
        Assert.AreEqual("add_number", result.Tools[0].Name);
    }

    [TestMethod("InProcess Disconnect: 断开连接后资源正确释放")]
    public async Task Disconnect()
    {
        var package = await TestMcpFactory.Shared.CreateSimpleInProcessAsync();

        await package.Client.ListToolsAsync();
        Assert.IsTrue(package.Client.IsConnected);

        await package.DisposeAsync();
    }

    [TestMethod("InProcess CallTool: 正常调用 add 工具")]
    public async Task CallTool()
    {
        await using var package = await TestMcpFactory.Shared.CreateFullInProcessAsync();
        var arguments = JsonSerializer.SerializeToElement(new { a = 10, b = 20 });

        var result = await package.Client.CallToolAsync("add", arguments);

        Assert.AreNotEqual(true, result.IsError);
        Assert.IsTrue(result.Content.Count > 0);
        Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
        var textContent = (TextContentBlock)result.Content[0];
        Assert.AreEqual("30", textContent.Text);
    }

    [TestMethod("InProcess Initialized: initialized 通知能到达服务端")]
    public async Task NotificationInitialized()
    {
        var initializedNotificationReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var package = await TestMcpFactory.Shared.CreateInProcessCoreAsync(
            builder => builder
                .WithTools(t => t.WithTool(() => new SimpleTool()))
                .WithRequestHandlers(server => new InitializedTrackingRequestHandlers(server, initializedNotificationReceived)));

        await package.Client.ListToolsAsync();

        await initializedNotificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(initializedNotificationReceived.Task.IsCompletedSuccessfully);
    }

    [TestMethod("InProcess Sampling: 服务端工具可向客户端发起采样请求")]
    public async Task ServerToolCanRequestSampling()
    {
        const string expectedResponseText = "Hello from InProcess sampling!";
        var samplingHandlerInvoked = false;

        await using var package = await TestMcpFactory.Shared.CreateInProcessCoreAsync(
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

        var toolArguments = JsonSerializer.SerializeToElement(new { message = "What's 2+2?" });
        var callResult = await package.Client.CallToolAsync("ask_llm", toolArguments);

        Assert.IsNotNull(callResult);
        Assert.IsFalse(callResult.IsError);
        Assert.IsTrue(samplingHandlerInvoked);

        var textContent = callResult.Content.OfType<TextContentBlock>().FirstOrDefault();
        Assert.IsNotNull(textContent);
        Assert.AreEqual(expectedResponseText, textContent.Text);
    }

    [TestMethod("InProcess ConcurrentRequests: 并发请求能按 id 正确匹配响应")]
    public async Task ConcurrentRequests()
    {
        await using var package = await TestMcpFactory.Shared.CreateFullInProcessAsync();

        var tasks = Enumerable.Range(0, 20)
            .Select(async index =>
            {
                var arguments = JsonSerializer.SerializeToElement(new { a = index, b = index + 1 });
                var result = await package.Client.CallToolAsync("add", arguments);
                var textContent = (TextContentBlock)result.Content[0];
                return int.Parse(textContent.Text);
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);

        CollectionAssert.AreEqual(Enumerable.Range(0, 20).Select(index => index + index + 1).ToArray(), results);
    }

    [TestMethod("InProcess Connect: 服务端未启动时可取消等待")]
    public async Task Connect_CancelWhenServerNotStarted()
    {
        await using var transportPair = new InProcessTransportPair();
        await using var client = new McpClientBuilder()
            .WithInProcess(transportPair)
            .Build();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var canceled = false;
        try
        {
            await client.ListToolsAsync(cancellationToken: cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        Assert.IsTrue(canceled, "服务端未启动时，客户端连接等待应能被取消。");
    }

    [TestMethod("InProcess ServerStops: 服务端停止后客户端请求不会永久挂起")]
    public async Task ServerStops_ClientRequestFailsFast()
    {
        await using var package = await TestMcpFactory.Shared.CreateSimpleInProcessAsync();
        await package.Client.ListToolsAsync();

        await package.Server.StopAsync();

        var failed = false;
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await package.Client.ListToolsAsync(cancellationToken: cancellationTokenSource.Token);
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException or ChannelClosedException)
        {
            failed = true;
        }

        Assert.IsTrue(failed, "服务端停止后，客户端请求应快速失败或被取消，而不是永久挂起。");
    }

    private sealed class InitializedTrackingRequestHandlers(
        McpServer server,
        TaskCompletionSource initializedNotificationReceived) : McpServerRequestHandlers(server)
    {
        protected override ValueTask OnNotificationReceivedAsync(JsonRpcRequest notification)
        {
            if (notification.Method == RequestMethods.NotificationsInitialized)
            {
                initializedNotificationReceived.TrySetResult();
            }

            return default;
        }
    }
}