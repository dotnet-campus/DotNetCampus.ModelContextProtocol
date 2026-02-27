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
        // 结束相关的 node.exe 进程（会有误杀，但无所谓了）：
        await Process.Start(new ProcessStartInfo(
            McpStdioUtils.ResolveCommandPath("taskkill")!,
            ["/F", "/IM", "node.exe"])
        {
            UseShellExecute = false,
        })!.WaitForExitAsync();

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

        try
        {
            Assert.IsNotNull(process, "Failed to start npx process.");
            await Task.Delay(2000);
            Assert.IsFalse(process.HasExited, "npx process exited unexpectedly.");

            // Act
            await using var client = new McpClientBuilder()
                .WithLogger(TestMcpFactory.DefaultLogger)
                .WithHttp($"http://localhost:{port}/mcp")
                .Build();
            var tools = await client.ListToolsAsync();

            // Assert
            Assert.IsNotNull(tools.Tools.Count > 0);
        }
        finally
        {
            // 结束进程本身。
            process?.Kill();

            await Task.Delay(1000);

            // 结束相关的 node.exe 进程（会有误杀，但无所谓了）：
            await Process.Start(new ProcessStartInfo(
                McpStdioUtils.ResolveCommandPath("taskkill")!,
                ["/F", "/IM", "node.exe"])
            {
                UseShellExecute = false,
            })!.WaitForExitAsync();
        }
    }
}
