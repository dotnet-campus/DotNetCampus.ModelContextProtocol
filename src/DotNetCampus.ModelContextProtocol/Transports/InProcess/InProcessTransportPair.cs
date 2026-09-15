using System.Threading.Channels;

namespace DotNetCampus.ModelContextProtocol.Transports.InProcess;

/// <summary>
/// 一条 In-Process 传输层连接对。
/// </summary>
internal sealed class InProcessTransportPair : IAsyncDisposable
{
    private readonly Channel<string> _clientToServer;
    private readonly Channel<string> _serverToClient;
    private int _completed;

    /// <summary>
    /// 初始化 <see cref="InProcessTransportPair"/> 类的新实例。
    /// </summary>
    /// <param name="options">传输层选项。</param>
    internal InProcessTransportPair(InProcessTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Capacity is { } capacity && capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "In-Process 传输层队列容量必须为正整数。");
        }

        _clientToServer = CreateChannel(options);
        _serverToClient = CreateChannel(options);
    }

    internal IAsyncEnumerable<string> ReadClientMessagesAsync(CancellationToken cancellationToken)
    {
        return _clientToServer.Reader.ReadAllAsync(cancellationToken);
    }

    internal IAsyncEnumerable<string> ReadServerMessagesAsync(CancellationToken cancellationToken)
    {
        return _serverToClient.Reader.ReadAllAsync(cancellationToken);
    }

    internal ValueTask SendToServerAsync(string message, CancellationToken cancellationToken)
    {
        return _clientToServer.Writer.WriteAsync(message, cancellationToken);
    }

    internal ValueTask SendToClientAsync(string message, CancellationToken cancellationToken)
    {
        return _serverToClient.Writer.WriteAsync(message, cancellationToken);
    }

    internal void CompleteClient(Exception? exception = null)
    {
        _clientToServer.Writer.TryComplete(exception);
    }

    internal void CompleteServer(Exception? exception = null)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }

        _clientToServer.Writer.TryComplete(exception);
        _serverToClient.Writer.TryComplete(exception);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        CompleteServer();
        return ValueTask.CompletedTask;
    }

    private static Channel<string> CreateChannel(InProcessTransportOptions options)
    {
        if (options.Capacity is { } capacity)
        {
            return Channel.CreateBounded<string>(new BoundedChannelOptions(capacity)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
        }

        return Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false,
        });
    }
}