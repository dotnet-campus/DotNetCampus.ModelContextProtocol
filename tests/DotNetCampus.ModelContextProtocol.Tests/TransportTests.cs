namespace DotNetCampus.ModelContextProtocol.Tests;

[TestClass]
public class TransportTests
{
    [TestMethod]
    public async Task Http()
    {
        // Arrange
        var package = await TestMcpFactory.Shared.CreateSimpleAsync();

        // Act
        var result = await package.Client.ListToolsAsync();

        // Assert
        Assert.AreEqual(1, result.Tools.Count);
    }
}
