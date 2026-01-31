using DotNetCampus.Logging;
using DotNetCampus.Logging.Writers;
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;
using DotNetCampus.ModelContextProtocol.Tests.McpTools;
using DotNetCampus.ModelContextProtocol.Transports.Http;
using DotNetCampus.ModelContextProtocol.Transports.TouchSocket;

namespace DotNetCampus.ModelContextProtocol.Tests;

public class TestMcpFactory
{
    private static volatile int _port = 20000;

    private static readonly Lazy<TestMcpFactory> SharedLazy = new(() => new TestMcpFactory(), LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<IMcpLogger> LoggerLazy = new Lazy<IMcpLogger>(() =>
    {
        var builder = new LoggerBuilder()
            .WithMemoryCache()
            .WithLevel(LogLevel.Trace)
            .AddConsoleLogger(b =>
            {
                b
                    .WithOutput(LoggerConsoleOutputTo.StandardError)
                    .WithThreadSafe(LogWritingThreadMode.NotThreadSafe)
                    .WithStdioRedirectedFormat();
                // Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            })
            .Build();
        var logger = builder.Logger;
        return new McpLoggerBridge(logger);
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    public static TestMcpFactory Shared => SharedLazy.Value;

    public static IMcpLogger DefaultLogger => LoggerLazy.Value;

    public async ValueTask<McpTestingPackage> CreateSimpleHttpAsync(HttpTransportType httpTransportType)
    {
        var port = Interlocked.Increment(ref _port);
        var mcpServerBuilder = new McpServerBuilder("TestMcpServer", "1.0.0")
            .WithLogger(DefaultLogger)
            .WithTools(t => t.WithTool(() => new SimpleTool()));
        mcpServerBuilder = httpTransportType switch
        {
            HttpTransportType.LocalHost => mcpServerBuilder.WithLocalHostHttp(new LocalHostHttpServerTransportOptions
            {
                Port = port,
                EndPoint = "mcp",
            }),
            HttpTransportType.TouchSocket => mcpServerBuilder.WithTouchSocketHttp(new TouchSocketHttpServerTransportOptions
            {
                Listen = [$"127.0.0.1:{port}", $"[::1]:{port}"],
                EndPoint = "mcp",
            }),
            _ => throw new NotSupportedException($"不支持的传输层类型：{httpTransportType}"),
        };
        var mcpServer = mcpServerBuilder.Build();
        mcpServer.EnableDebugMode();
        await mcpServer.StartAsync(CancellationToken.None);

        var mcpClient = new McpClientBuilder()
            .WithLogger(DefaultLogger)
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

public enum HttpTransportType
{
    LocalHost,
    TouchSocket,
}

file class McpLoggerBridge(ILogger logger) : IMcpLogger
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
