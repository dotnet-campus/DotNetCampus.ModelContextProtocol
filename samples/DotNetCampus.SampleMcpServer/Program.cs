using DotNetCampus.Logging;
using DotNetCampus.Logging.Attributes;
using DotNetCampus.Logging.Writers;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
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
                .WithOutput(LoggerConsoleOutputTo.StandardError)
                .WithThreadSafe(LogWritingThreadMode.ProducerConsumer)
                .FilterConsoleTagsFromCommandLineArgs(args))
            .AddBridge(LoggerBridgeLinker.Default)
            .Build()
            .IntoGlobalStaticLog();

        var mcpServer = new McpServerBuilder("SampleMcpServer", "1.0.0")
            .WithLogger(new McpLoggerBridge(Log.Current))
            // .WithLocalHostHttp(5943, "mcp")
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
            )
            .Build();
        mcpServer.EnableDebugMode();
        await mcpServer.RunAsync();
    }
}

[ImportLoggerBridge<IMcpLoggerBridge>]
internal partial class LoggerBridgeLinker;

internal class McpLoggerBridge(ILogger logger) : IMcpLogger
{
    public bool IsEnabled(LoggingLevel loggingLevel)
    {
        return logger.IsEnabled(loggingLevel.ToLogLevel());
    }

    public void Log<TState>(LoggingLevel loggingLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        logger.Log(loggingLevel.ToLogLevel(), default, state, exception, formatter);
    }
}

file static class LoggingExtensions
{
    extension(LoggingLevel level)
    {
        public LogLevel ToLogLevel() => level switch
        {
            LoggingLevel.Debug => LogLevel.Trace,
            LoggingLevel.Info or LoggingLevel.Notice => LogLevel.Information,
            LoggingLevel.Warning => LogLevel.Warning,
            LoggingLevel.Error => LogLevel.Error,
            LoggingLevel.Critical or LoggingLevel.Alert or LoggingLevel.Emergency => LogLevel.Critical,
            _ => LogLevel.None,
        };
    }
}
