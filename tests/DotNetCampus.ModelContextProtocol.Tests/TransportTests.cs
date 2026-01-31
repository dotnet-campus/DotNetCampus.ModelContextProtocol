namespace DotNetCampus.ModelContextProtocol.Tests;

[TestClass]
public class TransportTests
{
    [TestMethod]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "使用 LocalHostHttpServerTransport 作为传输层")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "使用 TouchSocketHttpServerTransport 作为传输层")]
    public async Task Http(HttpTransportType type)
    {
        // Arrange
        var package = await TestMcpFactory.Shared.CreateSimpleHttpAsync(type);

        // Act
        var result = await package.Client.ListToolsAsync();

        // Assert
        Assert.AreEqual(1, result.Tools.Count);
    }
}
