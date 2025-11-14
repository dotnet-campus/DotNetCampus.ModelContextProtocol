using DotNetCampus.Logging;
using DotNetCampus.Logging.Attributes;
using DotNetCampus.Logging.Writers;
using DotNetCampus.ModelContextProtocol.Servers;
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
            .WithTools(t => t
                .WithJsonSerializer(McpToolJsonContext.Default)
                .WithTool(() => new SampleTools())
            )
            .WithHttp(5943, "mcp")
            .WithStdio()
            .Build();
        mcpServer.EnableDebugMode();
        await mcpServer.RunAsync();
    }
}

[ImportLoggerBridge<DotNetCampus.ModelContextProtocol.Logging.ILoggerBridge>]
internal partial class LoggerBridgeLinker;
