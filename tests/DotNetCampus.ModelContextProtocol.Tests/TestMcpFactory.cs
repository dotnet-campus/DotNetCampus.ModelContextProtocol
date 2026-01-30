using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.Servers;
using DotNetCampus.ModelContextProtocol.Tests.McpTools;
using DotNetCampus.ModelContextProtocol.Transports.Http;

namespace DotNetCampus.ModelContextProtocol.Tests;

public class TestMcpFactory
{
    private static readonly Lazy<TestMcpFactory> SharedLazy = new(() => new TestMcpFactory(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static TestMcpFactory Shared => SharedLazy.Value;

    public async ValueTask<McpTestingPackage> CreateSimpleHttpAsync()
    {
        const int port = 16001;
        var mcpServer = new McpServerBuilder("TestMcpServer", "1.0.0")
            .WithLocalHostHttp(new LocalHostHttpServerTransportOptions
            {
                Port = port,
                EndPoint = "mcp",
            })
            .WithTools(t => t.WithTool(() => new SimpleTool()))
            .Build();
        mcpServer.EnableDebugMode();
        await mcpServer.StartAsync(CancellationToken.None);

        var mcpClient = new McpClientBuilder()
            .WithHttp($"http://127.0.0.1:{port}/mcp")
            .Build();

        return new McpTestingPackage(mcpServer, mcpClient);
    }
}

public class McpTestingPackage : IAsyncDisposable
{
    public McpTestingPackage(McpServer server, McpClient client)
    {
        Server = server;
        Client = client;
    }

    public McpServer Server { get; }

    public McpClient Client { get; }

    public async ValueTask DisposeAsync()
    {
        // 停止客户端。
        await Client.DisposeAsync();

        // 停止服务端。
        await Server.StopAsync();
    }
}
