using System.Diagnostics;
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.Transports.Stdio;

namespace DotNetCampus.ModelContextProtocol.Tests.Clients;

[TestClass]
public sealed class McpClientTests
{
    [TestMethod("使用 @modelcontextprotocol/server-everything STDIO：失败则证明客户端错误")]
    public async Task ServerEverything_Stdio()
    {
        // Arrange & Act
        await using var client = new McpClientBuilder()
            .WithStdio("npx", ["-y", "@modelcontextprotocol/server-everything", "stdio"])
            .Build();

        // Act
        var tools = await client.ListToolsAsync();

        // Assert
        Assert.IsNotNull(tools.Tools.Count > 0);
    }

    [TestMethod("使用 @modelcontextprotocol/server-everything HTTP：失败则证明客户端错误")]
    public async Task ServerEverything_Http()
    {
        // Arrange
        const string port = "15001";
        var process = Process.Start(new ProcessStartInfo(
            McpStdioUtils.ResolveCommandPath("npx")!,
            ["-y", "@modelcontextprotocol/server-everything", "streamableHttp"])
        {
            Environment =
            {
                ["PORT"] = port,
            },
        });
        await Task.Delay(2000);

        try
        {
            // Act
            await using var client = new McpClientBuilder()
                .WithHttp($"http://localhost:{port}/mcp")
                .Build();
            var tools = await client.ListToolsAsync();

            // Assert
            Assert.IsNotNull(tools.Tools.Count > 0);
        }
        finally
        {
            process?.Kill();
        }
    }
}
