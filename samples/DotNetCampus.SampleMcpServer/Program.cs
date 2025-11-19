using DotNetCampus.Logging;
using DotNetCampus.Logging.Attributes;
using DotNetCampus.Logging.Writers;
using DotNetCampus.ModelContextProtocol.Servers;
using DotNetCampus.SampleMcpServer.McpResources;
using DotNetCampus.SampleMcpServer.McpTools;

namespace DotNetCampus.SampleMcpServer;

internal class Program
{
    private static async Task Main(string[] args)
    {
        new LoggerBuilder()
            .WithMemoryCache()
            .WithLevel(LogLevel.Trace)
            .AddConsoleLogger(b => b
                .WithThreadSafe(LogWritingThreadMode.ProducerConsumer)
                .FilterConsoleTagsFromCommandLineArgs(args))
            .AddBridge(LoggerBridgeLinker.Default)
            .Build()
            .IntoGlobalStaticLog();

        Log.Info($"[App] Starting Sample MCP Server...");

        var mcpServer = new McpServerBuilder("SampleMcpServer", "1.0.0")
            .WithHttp(5943, "mcp")
            .WithStdio()
            .WithJsonSerializer(McpToolJsonContext.Default)
            .WithRequestHandlers((s, d) => new McpRequestHandlers(s)
            {
                ListToolsHandler = (request, token) => d.ListTools(request, token),
                CallToolHandler = (request, token) => d.CallTool(request, token),
            })
            .WithTools(t => t
                .WithTool(() => new SampleTool())
                .WithTool(() => new InputTool())
                .WithTool(() => new OutputTool())
                .WithTool(() => new PolymorphicTool())
                .WithTool(() => new ResourceTool())
            )
            .WithResources(r => r
                .WithResource(() => new SampleResource())
                .WithResource(() => new SampleResource())
                .WithResource(() => new SampleResource())
            )
            .Build();
        mcpServer.EnableDebugMode();
        await mcpServer.RunAsync();
    }
}

[ImportLoggerBridge<DotNetCampus.ModelContextProtocol.Logging.ILoggerBridge>]
internal partial class LoggerBridgeLinker;
