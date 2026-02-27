namespace DotNetCampus.ModelContextProtocol.Tests.Transports;

/// <summary>
/// HTTP 传输层特性测试：SSE 和 POST 通信模式的特定测试。
/// </summary>
[TestClass]
public class HttpTransportTests
{
    // TODO: 需要实现更底层的 HTTP 请求测试，不通过 McpClient 而是直接发送 HTTP 请求

    [TestMethod("Post_NoSessionId: 旧协议下不带 sessionId 应返回错误")]
    public async Task Post_NoSessionId()
    {
        // 此测试需要直接发送 HTTP 请求而非通过 McpClient
        // 目前作为占位符
        await Task.CompletedTask;
        Assert.Inconclusive("需要直接 HTTP 请求测试，待基础设施完善后实现。");
    }

    [TestMethod("Sse_EndpointEvent: 旧协议 SSE 连接应首先收到 endpoint 事件")]
    public async Task Sse_EndpointEvent()
    {
        // 此测试需要直接监听 SSE 流
        // 目前作为占位符
        await Task.CompletedTask;
        Assert.Inconclusive("需要直接 SSE 流监听测试，待基础设施完善后实现。");
    }

    [TestMethod("Delete_TerminateSession: 新协议 DELETE 请求应终止会话")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task Delete_TerminateSession(HttpTransportType type)
    {
        // Arrange
        var package = await TestMcpFactory.Shared.CreateSimpleHttpAsync(type);

        // 建立连接
        await package.Client.ListToolsAsync();
        Assert.IsTrue(package.Client.IsConnected);

        // Act - 断开连接（内部会发送 DELETE 请求）
        await package.DisposeAsync();

        // Assert - Client 应已断开
        Assert.IsFalse(package.Client.IsConnected);
    }
}
