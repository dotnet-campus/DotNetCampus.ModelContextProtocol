using DotNetCampus.ModelContextProtocol.Clients;

namespace DotNetCampus.ModelContextProtocol.Tests.Clients;

[TestClass]
public sealed class McpClientTests
{
    [TestMethod]
    public async Task Foo()
    {
        // Arrange & Act
        await using var client = new McpClientBuilder()
            .WithStdio("npx", ["-y", "@modelcontextprotocol/server-everything"])
            .Build();

        // Act
        var tools = await client.ListToolsAsync();

        // Assert
        Assert.IsNotNull(tools.Tools.Count > 0);
    }
}
