namespace DotNetCampus.ModelContextProtocol.Tests;

/// <summary>
/// 传输层测试：验证不同传输实现的连接建立和断开。
/// </summary>
[TestClass]
public class TransportTests
{
    [TestMethod("Http: 连接成功并能调用工具")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task Http(HttpTransportType type)
    {
        // Arrange
        await using var package = await TestMcpFactory.Shared.CreateSimpleHttpAsync(type);

        // Act
        var result = await package.Client.ListToolsAsync();

        // Assert
        Assert.AreEqual(1, result.Tools.Count);
    }

    [TestMethod("Http_Disconnect: 断开连接后资源正确释放")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task Http_Disconnect(HttpTransportType type)
    {
        // Arrange
        var package = await TestMcpFactory.Shared.CreateSimpleHttpAsync(type);

        // Act - 确保连接建立
        await package.Client.ListToolsAsync();
        Assert.IsTrue(package.Client.IsConnected);

        // Act - 断开连接
        await package.DisposeAsync();

        // Assert - 不抛异常即为成功
    }
}
