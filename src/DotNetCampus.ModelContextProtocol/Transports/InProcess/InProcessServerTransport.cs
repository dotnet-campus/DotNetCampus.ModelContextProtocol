using System.Collections.Concurrent;
using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Hosting.Services;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.InProcess;

/// <summary>
/// In-Process 服务端传输层，用于同进程内的 MCP 通信。支持多个客户端同时连接。
/// </summary>
public sealed class InProcessServerTransport : IServerTransport
{
    private readonly IServerTransportManager _manager;
    private readonly InProcessTransportOptions _options;
    private readonly ConcurrentDictionary<string, ClientConnection> _connections = [];
    private readonly TaskCompletionSource _runningTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationToken _runningCancellationToken;
    private int _started;

    /// <summary>
    /// 初始化 <see cref="InProcessServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    public InProcessServerTransport(IServerTransportManager manager) : this(manager, new InProcessTransportOptions())
    {
    }

    /// <summary>
    /// 初始化 <see cref="InProcessServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="options">In-Process 传输层选项。</param>
    public InProcessServerTransport(IServerTransportManager manager, InProcessTransportOptions options)
    {
        _manager = manager;
        _options = options;
    }

    private IMcpLogger Log => _manager.Context.Logger;

    /// <summary>
    /// 创建一个新的 In-Process 客户端连接。由客户端在构建时调用。
    /// </summary>
    /// <returns>用于连接的 <see cref="InProcessTransportPair"/>。</returns>
    internal InProcessTransportPair Connect()
    {
        if (Volatile.Read(ref _started) == 0)
        {
            throw new InvalidOperationException("In-Process 服务端传输层尚未启动，无法接受客户端连接。请先调用 McpServer.StartAsync()。");
        }

        var pair = new InProcessTransportPair(_options);
        pair.AttachServer();
        var session = new InProcessServerTransportSession(_manager, pair);
        var connectionId = _manager.MakeNewSessionId().Id;

        _manager.Add(session);
        _connections[connectionId] = new ClientConnection(pair, session);
        pair.MarkServerStarted();

        Log.Info($"[McpServer][InProcess] New client connected. ConnectionId={connectionId}");

        _ = RunLoopAsync(connectionId, pair, session, _runningCancellationToken);
        return pair;
    }

    /// <inheritdoc />
    public Task<Task> StartAsync(CancellationToken startingCancellationToken, CancellationToken runningCancellationToken)
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return Task.FromResult(Task.CompletedTask);
        }

        _runningCancellationToken = runningCancellationToken;
        runningCancellationToken.Register(() => _runningTaskCompletionSource.TrySetResult());

        Log.Info($"[McpServer][InProcess] Transport started, waiting for client connections.");

        return Task.FromResult<Task>(_runningTaskCompletionSource.Task);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Log.Info($"[McpServer][InProcess] Disposing transport.");

        foreach (var (_, connection) in _connections)
        {
            connection.Pair.CompleteServer();
            await connection.Session.DisposeAsync().ConfigureAwait(false);
        }
        _connections.Clear();
    }

    private async Task RunLoopAsync(string connectionId, InProcessTransportPair pair, InProcessServerTransportSession session, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var line in pair.ReadClientMessagesAsync(cancellationToken).ConfigureAwait(false))
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
                    message = null;
                }

                switch (message)
                {
                    case JsonRpcResponse response:
                        Log.Debug($"[McpServer][InProcess] Routing client response to session. ConnectionId={connectionId}");
                        session.HandleResponseAsync(response);
                        continue;

                    case JsonRpcNotification notification:
                        _ = HandleNotificationAsync(session, notification, cancellationToken);
                        continue;

                    case JsonRpcRequest request:
                        _ = HandleRequestAsync(session, request, cancellationToken);
                        continue;

                    default:
                        Log.Warn($"[McpServer][InProcess] Received unrecognizable message, responding with error. ConnectionId={connectionId}");
                        await session.SendMessageAsync(new JsonRpcResponse
                        {
                            Error = new JsonRpcError
                            {
                                Code = (int)JsonRpcErrorCode.InvalidRequest,
                                Message = $"Invalid request message: {line}",
                            },
                        }, cancellationToken).ConfigureAwait(false);
                        continue;
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
            Log.Error($"[McpServer][InProcess] Error in transport loop. ConnectionId={connectionId}", ex);
            pair.CompleteServer(ex);
            return;
        }
        finally
        {
            Log.Info($"[McpServer][InProcess] Client disconnected. ConnectionId={connectionId}");
            pair.CompleteServer();
            await session.DisposeAsync().ConfigureAwait(false);
            _connections.TryRemove(connectionId, out _);
        }
    }

    private async Task HandleNotificationAsync(InProcessServerTransportSession session, JsonRpcNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await _manager.HandleRequestAsync(
                new JsonRpcRequest { Method = notification.Method, Params = notification.Params },
                services => services.AddTransportSession(session, Log),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][InProcess] Error handling client notification.", ex);
        }
    }

    private async Task HandleRequestAsync(InProcessServerTransportSession session, JsonRpcRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _manager.HandleRequestAsync(
                request,
                services => services.AddTransportSession(session, Log),
                cancellationToken).ConfigureAwait(false);
            if (response is not null)
            {
                await session.SendMessageAsync(response, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][InProcess] Error handling client request. Method={request.Method}, Id={request.Id}", ex);
            try
            {
                await session.SendMessageAsync(new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = (int)JsonRpcErrorCode.InternalError,
                        Message = ex.Message,
                    },
                }, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // 连接可能已经关闭，无法再发送错误响应。
            }
        }
    }

    private record ClientConnection(InProcessTransportPair Pair, InProcessServerTransportSession Session);
}