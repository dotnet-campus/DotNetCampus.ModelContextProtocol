using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.InProcess;

/// <summary>
/// In-Process 客户端传输层，用于同进程内的 MCP 通信。
/// </summary>
public sealed class InProcessClientTransport : IClientTransport
{
    private readonly IClientTransportManager _manager;
    private readonly InProcessServerTransport _serverTransport;
    private InProcessTransportPair? _transportPair;
    private CancellationTokenSource? _disconnectCancellationTokenSource;
    private Task? _runLoopTask;
    private int _connected;

    /// <summary>
    /// 初始化 <see cref="InProcessClientTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="serverTransport">In-Process 服务端传输层，连接将在 <see cref="ConnectAsync"/> 中建立。</param>
    internal InProcessClientTransport(IClientTransportManager manager, InProcessServerTransport serverTransport)
    {
        _manager = manager;
        _serverTransport = serverTransport;
    }

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _connected, 1, 0) != 0)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            Log.Info($"[McpClient][InProcess] Transport started.");

            var pair = _serverTransport.Connect();
            _transportPair = pair;

            _disconnectCancellationTokenSource = new CancellationTokenSource();
            _runLoopTask = RunLoopAsync(_disconnectCancellationTokenSource.Token);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _connected, 0);
            return ValueTask.FromException(ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _connected, 0) == 0)
        {
            return;
        }

        _transportPair?.CompleteClient();

        var cancellationTokenSource = _disconnectCancellationTokenSource;
        if (cancellationTokenSource is not null)
        {
            cancellationTokenSource.Cancel();
        }

        CancelAllPendingRequests();

        if (_runLoopTask is not null)
        {
            try
            {
                await _runLoopTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ChannelClosedException)
            {
            }
        }

        cancellationTokenSource?.Dispose();
        _disconnectCancellationTokenSource = null;
        _runLoopTask = null;
    }

    /// <inheritdoc />
    public async ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _connected) == 0)
        {
            throw new InvalidOperationException("In-Process 传输层尚未连接或已经断开，无法发送消息。");
        }

        var line = _manager.WriteMessageAsync(message);
        _manager.LogRawOut("[InProcess]", line);
        await _transportPair!.SendToServerAsync(line, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var line in _transportPair!.ReadServerMessagesAsync(cancellationToken).ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                _manager.LogRawIn("[InProcess]", line);

                JsonRpcMessage? message;
                try
                {
                    message = await _manager.ReadMessageAsync(line).ConfigureAwait(false);
                }
                catch
                {
                    Log.Warn($"[McpClient][InProcess] Invalid server message received.");
                    continue;
                }

                switch (message)
                {
                    case JsonRpcRequest request:
                        await _manager.HandleServerRequestAsync(request, cancellationToken).ConfigureAwait(false);
                        break;
                    case JsonRpcResponse response:
                        await _manager.HandleRespondAsync(response, cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        Log.Warn($"[McpClient][InProcess] Unrecognized server message received.");
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ChannelClosedException)
        {
        }
        catch (Exception ex)
        {
            Log.Error($"[McpClient][InProcess] Error in transport loop.", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _connected, 0);
            CancelAllPendingRequests();
        }
    }

    private void CancelAllPendingRequests()
    {
        if (_manager is ClientTransportManager manager)
        {
            manager.CancelAllPendingRequests();
        }
    }
}