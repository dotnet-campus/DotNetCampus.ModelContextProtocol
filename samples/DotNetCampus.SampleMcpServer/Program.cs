using System.Text.Json;
using DotNetCampus.Logging;
using DotNetCampus.Logging.Attributes;
using DotNetCampus.Logging.Writers;
using DotNetCampus.ModelContextProtocol.Servers;

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

        Console.WriteLine("Starting Sample MCP Server...");

        var httpTransport = McpServer.CreateHttpServerTransport("http://127.0.0.1:5943/");

        await httpTransport.StartAsync();
    }
}

[ImportLoggerBridge<DotNetCampus.ModelContextProtocol.Logging.ILoggerBridge>]
internal partial class LoggerBridgeLinker;
