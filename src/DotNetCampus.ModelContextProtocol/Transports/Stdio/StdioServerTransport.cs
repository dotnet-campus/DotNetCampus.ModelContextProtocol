using System.Diagnostics;
using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Stdio;

public class StdioServerTransport : IServerTransport
{
    private readonly IServerTransportManager _manager;

    public StdioServerTransport(IServerTransportManager manager)
    {
        _manager = manager;
    }

    private IMcpLogger Log => _manager.Context.Logger;

    public Task<Task> StartAsync(CancellationToken cancellationToken = default)
    {
#if DEBUG
        Debugger.Launch();
#endif

        Log.Info($"[McpServer][Stdio] Starting STDIO server transport.");

        return Task.FromResult(RunLoopAsync(cancellationToken));
    }

    public ValueTask DisposeAsync()
    {
        Debug.WriteLine("Disposing StdioServerTransport");
        return ValueTask.CompletedTask;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        var standardInput = Console.OpenStandardInput();
        while (!cancellationToken.IsCancellationRequested)
        {
            var input = await _manager.ParseRequestStreamAsync(standardInput);

        }
    }
}

public class StdioServerTransportSession : IServerTransportSession
{
    private readonly Channel<JsonRpcMessage> _channel;

    public StdioServerTransportSession()
    {
        _channel = Channel.CreateUnbounded<JsonRpcMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>
    /// STDIO 传输层是专用的，不需要会话 ID。
    /// </summary>
    public string? SessionId => null;

    /// <inheritdoc />
    public ChannelReader<JsonRpcMessage> MessageReader => _channel.Reader;

    /// <inheritdoc />
    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
