namespace DotNetCampus.ModelContextProtocol.Tests;

[TestClass]
public sealed class McpClientTests
{
    [TestMethod]
    public void Constructor_ShouldCreateInstance()
    {
        // Arrange & Act
        var client = new McpClient();

        // Assert
        Assert.IsNotNull(client);
    }
}
