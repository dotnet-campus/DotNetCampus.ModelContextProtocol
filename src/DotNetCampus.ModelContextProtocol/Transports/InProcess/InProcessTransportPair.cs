using System.Threading.Channels;

namespace DotNetCampus.ModelContextProtocol.Transports.InProcess;

/// <summary>
/// 一条 In-Process 传输层连接对。
/// </summary>
public sealed class InProcessTransportPair : IAsyncDisposable
{
    private readonly Channel<string> _clientToServer;
    private readonly Channel<string> _serverToClient;
    private readonly TaskCompletionSource _serverStartedTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _clientAttached;
    private int _serverAttached;
    private int _completed;

    /// <summary>
    /// 初始化 <see cref="InProcessTransportPair"/> 类的新实例。
    /// </summary>
    public InProcessTransportPair() : this(new InProcessTransportOptions())
    {
    }

    /// <summary>
    /// 初始化 <see cref="InProcessTransportPair"/> 类的新实例。
    /// </summary>
    /// <param name="options">传输层选项。</param>
    public InProcessTransportPair(InProcessTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Capacity is { } capacity && capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "In-Process 传输层队列容量必须为正整数。");
        }

        _clientToServer = CreateChannel(options);
        _serverToClient = CreateChannel(options);
    }

    internal void AttachClient()
    {
        if (Interlocked.CompareExchange(ref _clientAttached, 1, 0) != 0)
        {
            throw new InvalidOperationException("此 In-Process 传输层连接对已经绑定了客户端传输层。");
        }
    }

    internal void AttachServer()
    {
        if (Interlocked.CompareExchange(ref _serverAttached, 1, 0) != 0)
        {
            throw new InvalidOperationException("此 In-Process 传输层连接对已经绑定了服务端传输层。");
        }
    }

    internal void MarkServerStarted()
    {
        _serverStartedTaskCompletionSource.TrySetResult();
    }

    internal Task WaitForServerStartedAsync(CancellationToken cancellationToken)
    {
        return _serverStartedTaskCompletionSource.Task.WaitAsync(cancellationToken);
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

        _serverStartedTaskCompletionSource.TrySetException(exception ?? new ObjectDisposedException(nameof(InProcessTransportPair)));
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