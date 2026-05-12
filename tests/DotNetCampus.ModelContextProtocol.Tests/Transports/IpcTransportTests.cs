using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Servers;
using DotNetCampus.ModelContextProtocol.Tests.McpTools;

namespace DotNetCampus.ModelContextProtocol.Tests.Transports;

/// <summary>
/// DotNetCampus.Ipc 传输层测试。
/// </summary>
[TestClass]
public class IpcTransportTests
{
    [TestMethod("Ipc Connect: 连接成功并能调用工具")]
    public async Task Connect()
    {
        await using var package = await TestMcpFactory.Shared.CreateSimpleIpcAsync();

        var result = await package.Client.ListToolsAsync();

        Assert.IsTrue(package.Client.IsConnected);
        Assert.AreEqual(1, result.Tools.Count);
        Assert.AreEqual("add_number", result.Tools[0].Name);
    }

    [TestMethod("Ipc Disconnect: 断开连接后资源正确释放")]
    public async Task Disconnect()
    {
        var package = await TestMcpFactory.Shared.CreateSimpleIpcAsync();

        await package.Client.ListToolsAsync();
        Assert.IsTrue(package.Client.IsConnected);

        await package.DisposeAsync();
    }

    [TestMethod("Ipc CallTool: 正常调用 add 工具")]
    public async Task CallTool()
    {
        await using var package = await TestMcpFactory.Shared.CreateFullIpcAsync();
        var arguments = JsonSerializer.SerializeToElement(new { a = 10, b = 20 });

        var result = await package.Client.CallToolAsync("add", arguments);

        Assert.AreNotEqual(true, result.IsError);
        Assert.IsTrue(result.Content.Count > 0);
        Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
        var textContent = (TextContentBlock)result.Content[0];
        Assert.AreEqual("30", textContent.Text);
    }

    [TestMethod("Ipc Initialized: initialized 通知能到达服务端")]
    public async Task NotificationInitialized()
    {
        var initializedNotificationReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var package = await TestMcpFactory.Shared.CreateIpcCoreAsync(
            builder => builder
                .WithTools(t => t.WithTool(() => new SimpleTool()))
                .WithRequestHandlers(server => new InitializedTrackingRequestHandlers(server, initializedNotificationReceived)));

        await package.Client.ListToolsAsync();

        await initializedNotificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(initializedNotificationReceived.Task.IsCompletedSuccessfully);
    }

    [TestMethod("Ipc Sampling: 服务端工具可向客户端发起采样请求")]
    public async Task ServerToolCanRequestSampling()
    {
        const string expectedResponseText = "Hello from IPC sampling!";
        var samplingHandlerInvoked = false;

        await using var package = await TestMcpFactory.Shared.CreateIpcCoreAsync(
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

    [TestMethod("Ipc ConcurrentRequests: 并发请求能按 id 正确匹配响应")]
    public async Task ConcurrentRequests()
    {
        await using var package = await TestMcpFactory.Shared.CreateFullIpcAsync();

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

    [TestMethod("Ipc MultiClient: 多个客户端同时连接同一服务器并独立调用工具")]
    public async Task MultiClient_IndependentToolCalls()
    {
        var pipeName = $"McpTest-{Guid.NewGuid():N}";
        var server = new McpServerBuilder("TestMcpServer", "1.0.0")
            .WithLogger(TestMcpFactory.DefaultLogger)
            .WithDotNetCampusIpc(pipeName)
            .WithTools(t => t.WithTool(() => new CalculatorTool()))
            .Build();
        server.EnableDebugMode();
        await server.StartAsync();

        try
        {
            // 创建两个独立的客户端。
            await using var client1 = new McpClientBuilder("test-client-1", "1.0.0")
                .WithLogger(TestMcpFactory.DefaultLogger)
                .WithDotNetCampusIpc(pipeName)
                .Build();
            await using var client2 = new McpClientBuilder("test-client-2", "1.0.0")
                .WithLogger(TestMcpFactory.DefaultLogger)
                .WithDotNetCampusIpc(pipeName)
                .Build();

            // 两个客户端独立调用工具。
            var args1 = JsonSerializer.SerializeToElement(new { a = 10, b = 20 });
            var args2 = JsonSerializer.SerializeToElement(new { a = 100, b = 200 });

            var result1Task = client1.CallToolAsync("add", args1);
            var result2Task = client2.CallToolAsync("add", args2);

            var result1 = await result1Task;
            var result2 = await result2Task;

            Assert.AreEqual("30", ((TextContentBlock)result1.Content[0]).Text);
            Assert.AreEqual("300", ((TextContentBlock)result2.Content[0]).Text);
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [TestMethod("Ipc MultiClient: 一个客户端断开不影响其他客户端")]
    public async Task MultiClient_DisconnectOneDoesNotAffectOthers()
    {
        var pipeName = $"McpTest-{Guid.NewGuid():N}";
        var server = new McpServerBuilder("TestMcpServer", "1.0.0")
            .WithLogger(TestMcpFactory.DefaultLogger)
            .WithDotNetCampusIpc(pipeName)
            .WithTools(t => t.WithTool(() => new CalculatorTool()))
            .Build();
        server.EnableDebugMode();
        await server.StartAsync();

        try
        {
            var client1 = new McpClientBuilder("test-client-1", "1.0.0")
                .WithLogger(TestMcpFactory.DefaultLogger)
                .WithDotNetCampusIpc(pipeName)
                .Build();
            await using var client2 = new McpClientBuilder("test-client-2", "1.0.0")
                .WithLogger(TestMcpFactory.DefaultLogger)
                .WithDotNetCampusIpc(pipeName)
                .Build();

            // 确认两个客户端都能正常调用。
            var args = JsonSerializer.SerializeToElement(new { a = 1, b = 2 });
            await client1.CallToolAsync("add", args);
            await client2.CallToolAsync("add", args);

            // 断开客户端 1。
            await client1.DisposeAsync();

            // 客户端 2 仍然可以正常调用。
            var result = await client2.CallToolAsync("add", args);
            Assert.AreEqual("3", ((TextContentBlock)result.Content[0]).Text);
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [TestMethod("Ipc BuildBeforeConnect: 先创建客户端再启动服务器后可正常调用")]
    public async Task BuildBeforeConnect_CanCallAfterServerStarts()
    {
        var pipeName = $"McpTest-{Guid.NewGuid():N}";
        var server = new McpServerBuilder("TestMcpServer", "1.0.0")
            .WithLogger(TestMcpFactory.DefaultLogger)
            .WithDotNetCampusIpc(pipeName)
            .WithTools(t => t.WithTool(() => new CalculatorTool()))
            .Build();

        // 在服务器启动之前就创建客户端。
        await using var client = new McpClientBuilder("test-client", "1.0.0")
            .WithLogger(TestMcpFactory.DefaultLogger)
            .WithDotNetCampusIpc(pipeName)
            .Build();

        // 此时服务器尚未启动，客户端已经存在。
        // 启动服务器。
        server.EnableDebugMode();
        await server.StartAsync();

        try
        {
            // 现在客户端可以正常调用工具。
            var args = JsonSerializer.SerializeToElement(new { a = 42, b = 58 });
            var result = await client.CallToolAsync("add", args);
            Assert.AreEqual("100", ((TextContentBlock)result.Content[0]).Text);
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [TestMethod("Ipc BuildBeforeConnect: 多个客户端先创建再启动服务器后可并发调用")]
    public async Task BuildBeforeConnect_MultipleClientsCanCallAfterServerStarts()
    {
        var pipeName = $"McpTest-{Guid.NewGuid():N}";
        var server = new McpServerBuilder("TestMcpServer", "1.0.0")
            .WithLogger(TestMcpFactory.DefaultLogger)
            .WithDotNetCampusIpc(pipeName)
            .WithTools(t => t.WithTool(() => new CalculatorTool()))
            .Build();

        // 在服务器启动之前创建多个客户端——这是全异步请求的关键场景。
        await using var client1 = new McpClientBuilder("test-client-1", "1.0.0")
            .WithLogger(TestMcpFactory.DefaultLogger)
            .WithDotNetCampusIpc(pipeName)
            .Build();
        await using var client2 = new McpClientBuilder("test-client-2", "1.0.0")
            .WithLogger(TestMcpFactory.DefaultLogger)
            .WithDotNetCampusIpc(pipeName)
            .Build();

        // 启动服务器。
        server.EnableDebugMode();
        await server.StartAsync();

        try
        {
            // 两个客户端并发调用。
            var args1 = JsonSerializer.SerializeToElement(new { a = 1, b = 2 });
            var args2 = JsonSerializer.SerializeToElement(new { a = 10, b = 20 });
            var task1 = client1.CallToolAsync("add", args1);
            var task2 = client2.CallToolAsync("add", args2);
            var result1 = await task1;
            var result2 = await task2;
            Assert.AreEqual("3", ((TextContentBlock)result1.Content[0]).Text);
            Assert.AreEqual("30", ((TextContentBlock)result2.Content[0]).Text);
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [TestMethod("Ipc Connect: 服务端未启动时客户端首次请求会抛出异常")]
    public async Task Connect_ThrowsWhenServerNotStarted()
    {
        var pipeName = $"McpTest-{Guid.NewGuid():N}";
        var server = new McpServerBuilder("TestMcpServer", "1.0.0")
            .WithDotNetCampusIpc(pipeName)
            .WithTools(t => t.WithTool(() => new SimpleTool()))
            .Build();
        // 没有调用 server.StartAsync()，因此 IPC 传输层尚未启动。

        // Build 不再抛出异常，客户端可以在服务器启动前创建。
        await using var client = new McpClientBuilder("test-client", "1.0.0")
            .WithDotNetCampusIpc(pipeName)
            .Build();

        // 首次 API 调用时触发连接，此时服务器未启动。
        // IPC 传输层（dotnetCampus.Ipc）连接无内置超时，需使用 WaitAsync 限制等待时间。
        await Assert.ThrowsExceptionAsync<TimeoutException>(async () =>
        {
            await client.CallToolAsync("add", default).WaitAsync(TimeSpan.FromSeconds(5));
        });
    }

    [TestMethod("Ipc EnsureConnectedAsync: 可提前验证连接并过滤不可用服务")]
    public async Task EnsureConnectedAsync_CanFilterUnavailableServers()
    {
        var goodPipeName = $"McpTest-Good-{Guid.NewGuid():N}";
        var badPipeName = $"McpTest-Bad-{Guid.NewGuid():N}";
        var goodServer = new McpServerBuilder("GoodServer", "1.0.0")
            .WithLogger(TestMcpFactory.DefaultLogger)
            .WithDotNetCampusIpc(goodPipeName)
            .WithTools(t => t.WithTool(() => new CalculatorTool()))
            .Build();
        var badServer = new McpServerBuilder("BadServer", "1.0.0")
            .WithDotNetCampusIpc(badPipeName)
            .Build();

        // 只启动 goodServer，badServer 保持未启动。
        goodServer.EnableDebugMode();
        await goodServer.StartAsync();

        try
        {
            var goodClient = new McpClientBuilder("test-client-good", "1.0.0")
                .WithLogger(TestMcpFactory.DefaultLogger)
                .WithDotNetCampusIpc(goodPipeName)
                .Build();
            var badClient = new McpClientBuilder("test-client-bad", "1.0.0")
                .WithDotNetCampusIpc(badPipeName)
                .Build();

            // 使用 EnsureConnectedAsync 提前过滤不可用的服务。
            // IPC 传输层（dotnetCampus.Ipc）连接无内置超时，需使用 WaitAsync 限制等待时间。
            var clients = new[] { goodClient, badClient };
            var available = new List<McpClient>();
            foreach (var client in clients)
            {
                try
                {
                    await client.EnsureConnectedAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    available.Add(client);
                }
                catch
                {
                    await client.DisposeAsync();
                }
            }

            // 只有 goodClient 可用。
            Assert.AreEqual(1, available.Count);
            Assert.IsTrue(available[0].IsConnected);

            // 可用的客户端能正常调用工具。
            var args = JsonSerializer.SerializeToElement(new { a = 7, b = 8 });
            var result = await available[0].CallToolAsync("add", args);
            Assert.AreEqual("15", ((TextContentBlock)result.Content[0]).Text);

            await available[0].DisposeAsync();
        }
        finally
        {
            await goodServer.StopAsync();
        }
    }

    [TestMethod("Ipc EnsureConnectedAsync: 多次调用是幂等的")]
    public async Task EnsureConnectedAsync_IsIdempotent()
    {
        await using var package = await TestMcpFactory.Shared.CreateFullIpcAsync();

        // EnsureConnectedAsync 多次调用不会报错，也不会重复连接。
        await package.Client.EnsureConnectedAsync();
        await package.Client.EnsureConnectedAsync();
        Assert.IsTrue(package.Client.IsConnected);

        // 调用后仍能正常使用。
        var args = JsonSerializer.SerializeToElement(new { a = 3, b = 4 });
        var result = await package.Client.CallToolAsync("add", args);
        Assert.AreEqual("7", ((TextContentBlock)result.Content[0]).Text);
    }

    [TestMethod("Ipc ServerStops: 服务端停止后客户端请求不会永久挂起")]
    public async Task ServerStops_ClientRequestFailsFast()
    {
        await using var package = await TestMcpFactory.Shared.CreateSimpleIpcAsync();
        await package.Client.ListToolsAsync();

        await package.Server.StopAsync();

        var failed = false;
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await package.Client.ListToolsAsync(cancellationToken: cancellationTokenSource.Token);
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
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
