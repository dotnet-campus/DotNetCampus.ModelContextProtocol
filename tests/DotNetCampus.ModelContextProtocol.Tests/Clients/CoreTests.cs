using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Tests.Clients;

/// <summary>
/// 核心功能集成测试：验证 Client 与 Server 的完整交互流程。
/// </summary>
[TestClass]
public class CoreTests
{
    #region 1.1 初始化与握手

    [TestMethod("Initialize: 成功握手并返回 ServerInfo")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task Initialize(HttpTransportType transportType)
    {
        // Arrange
        await using var package = await TestMcpFactory.Shared.CreateFullHttpAsync(transportType);

        // Act - ListToolsAsync 内部会自动触发 Initialize
        var result = await package.Client.ListToolsAsync();

        // Assert
        Assert.IsTrue(package.Client.IsConnected);
        Assert.IsNotNull(package.Client.ServerInfo);
        Assert.IsNotNull(package.Client.ServerInfo.ServerInfo);
        Assert.AreEqual("TestMcpServer", package.Client.ServerInfo.ServerInfo.Name);
    }

    [TestMethod("Ping: 握手后连接状态正常")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task Ping(HttpTransportType transportType)
    {
        // Arrange
        await using var package = await TestMcpFactory.Shared.CreateFullHttpAsync(transportType);

        // 确保先建立连接
        await package.Client.ListToolsAsync();

        // Assert
        Assert.IsTrue(package.Client.IsConnected);
    }

    #endregion

    #region 1.2 工具调用

    [TestMethod("ListTools: 返回所有已注册的工具")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task ListTools(HttpTransportType transportType)
    {
        // Arrange
        await using var package = await TestMcpFactory.Shared.CreateFullHttpAsync(transportType);

        // Act
        var result = await package.Client.ListToolsAsync();

        // Assert
        // 工具名称采用 snake_case 格式（由源生成器自动转换）
        Assert.IsTrue(result.Tools.Count > 0, "工具列表不应为空");
        Assert.IsTrue(result.Tools.Any(t => t.Name == "add"), "应包含 add 工具");
        Assert.IsTrue(result.Tools.Any(t => t.Name == "echo"), "应包含 echo 工具");
        Assert.IsTrue(result.Tools.Any(t => t.Name == "throw_error"), "应包含 throw_error 工具");
        Assert.IsTrue(result.Tools.Any(t => t.Name == "generate"), "应包含 generate 工具");
    }

    [TestMethod("CallTool: 正常调用 add 工具")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task CallTool(HttpTransportType transportType)
    {
        // Arrange
        await using var package = await TestMcpFactory.Shared.CreateFullHttpAsync(transportType);
        var args = JsonSerializer.SerializeToElement(new { a = 10, b = 20 });

        // Act
        var result = await package.Client.CallToolAsync("add", args);

        // Assert
        Assert.AreNotEqual(true, result.IsError);
        Assert.IsTrue(result.Content.Count > 0);
        Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
        var textContent = (TextContentBlock)result.Content[0];
        Assert.AreEqual("30", textContent.Text);
    }

    [TestMethod("CallTool_ComplexObject: 传递复杂对象参数")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task CallTool_ComplexObject(HttpTransportType transportType)
    {
        // Arrange
        await using var package = await TestMcpFactory.Shared.CreateFullHttpAsync(transportType);
        var args = JsonSerializer.SerializeToElement(new
        {
            user = new { Name = "TestUser", Id = 123 }
        });

        // Act
        var result = await package.Client.CallToolAsync("echo_user", args);

        // Assert
        Assert.AreNotEqual(true, result.IsError);
        Assert.IsTrue(result.Content.Count > 0);
        Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
        var textContent = (TextContentBlock)result.Content[0];
        StringAssert.Contains(textContent.Text, "TestUser");
        StringAssert.Contains(textContent.Text, "123");
    }

    [TestMethod("CallTool_MissingArgs: 缺少必需参数时返回错误")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task CallTool_MissingArgs(HttpTransportType transportType)
    {
        // Arrange
        await using var package = await TestMcpFactory.Shared.CreateFullHttpAsync(transportType);
        var args = JsonSerializer.SerializeToElement(new { a = 10 }); // 缺少 b 参数

        // Act
        var result = await package.Client.CallToolAsync("add", args);

        // Assert
        Assert.IsTrue(result.IsError == true, "缺少参数时应返回错误");
        Assert.IsTrue(result.Content.Count > 0);
        Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
    }

    [TestMethod("CallTool_ImplementationThrows: 工具抛出异常时返回 isError=true")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task CallTool_ImplementationThrows(HttpTransportType transportType)
    {
        // Arrange
        await using var package = await TestMcpFactory.Shared.CreateFullHttpAsync(transportType);
        var args = JsonSerializer.SerializeToElement(new { message = "Custom error message" });

        // Act
        var result = await package.Client.CallToolAsync("throw_error", args);

        // Assert
        Assert.IsTrue(result.IsError == true, "工具抛异常时应返回 isError=true");
        Assert.IsTrue(result.Content.Count > 0);
        Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
        var textContent = (TextContentBlock)result.Content[0];
        StringAssert.Contains(textContent.Text, "Custom error message");
    }

    [TestMethod("CallTool_LongOutput: 处理大量输出数据")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task CallTool_LongOutput(HttpTransportType transportType)
    {
        // Arrange
        await using var package = await TestMcpFactory.Shared.CreateFullHttpAsync(transportType);
        const int expectedLength = 100_000; // 100KB
        var args = JsonSerializer.SerializeToElement(new { length = expectedLength });

        // Act
        var result = await package.Client.CallToolAsync("generate", args);

        // Assert
        Assert.AreNotEqual(true, result.IsError);
        Assert.IsTrue(result.Content.Count > 0);
        Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
        var textContent = (TextContentBlock)result.Content[0];
        Assert.AreEqual(expectedLength, textContent.Text.Length);
    }

    [TestMethod("CallTool_NonExistent: 调用不存在的工具时返回错误")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task CallTool_NonExistent(HttpTransportType transportType)
    {
        // Arrange
        await using var package = await TestMcpFactory.Shared.CreateFullHttpAsync(transportType);

        // Act
        var result = await package.Client.CallToolAsync("non_existent_tool");

        // Assert
        Assert.IsTrue(result.IsError == true, "不存在的工具应返回错误");
        Assert.IsTrue(result.Content.Count > 0);
        Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
        var textContent = (TextContentBlock)result.Content[0];
        StringAssert.Contains(textContent.Text, "non_existent_tool");
    }

    [TestMethod("CallTool_Echo: 简单字符串回声")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task CallTool_Echo(HttpTransportType transportType)
    {
        // Arrange
        await using var package = await TestMcpFactory.Shared.CreateFullHttpAsync(transportType);
        const string testMessage = "Hello, MCP!";
        var args = JsonSerializer.SerializeToElement(new { message = testMessage });

        // Act
        var result = await package.Client.CallToolAsync("echo", args);

        // Assert
        Assert.AreNotEqual(true, result.IsError);
        Assert.IsTrue(result.Content.Count > 0);
        Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
        var textContent = (TextContentBlock)result.Content[0];
        Assert.AreEqual(testMessage, textContent.Text);
    }

    #endregion
}
