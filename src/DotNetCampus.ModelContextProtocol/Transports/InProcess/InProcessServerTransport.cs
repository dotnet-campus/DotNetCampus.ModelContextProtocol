using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Hosting.Services;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.InProcess;

/// <summary>
/// In-Process 服务端传输层，用于同进程内的 MCP 通信。
/// </summary>
public sealed class InProcessServerTransport : IServerTransport
{
    private readonly IServerTransportManager _manager;
    private readonly InProcessTransportPair _transportPair;
    private readonly InProcessServerTransportSession _session;
    private int _started;

    /// <summary>
    /// 初始化 <see cref="InProcessServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="transportPair">In-Process 传输层连接对。</param>
    public InProcessServerTransport(IServerTransportManager manager, InProcessTransportPair transportPair)
    {
        _manager = manager;
        _transportPair = transportPair;
        _transportPair.AttachServer();
        _session = new InProcessServerTransportSession(manager, transportPair);
    }

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public Task<Task> StartAsync(CancellationToken startingCancellationToken, CancellationToken runningCancellationToken)
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return Task.FromResult(Task.CompletedTask);
        }

        Log.Info($"[McpServer][InProcess] Transport started.");

        _manager.Add(_session);
        _transportPair.MarkServerStarted();
        return Task.FromResult(RunLoopAsync(runningCancellationToken));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Log.Info($"[McpServer][InProcess] Disposing transport.");

        _transportPair.CompleteServer();
        await _session.DisposeAsync().ConfigureAwait(false);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var line in _transportPair.ReadClientMessagesAsync(cancellationToken).ConfigureAwait(false))
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
                        Log.Debug($"[McpServer][InProcess] Routing client response to session.");
                        _session.HandleResponseAsync(response);
                        continue;

                    case JsonRpcNotification notification:
                        _ = HandleNotificationAsync(notification, cancellationToken);
                        continue;

                    case JsonRpcRequest request:
                        _ = HandleRequestAsync(request, cancellationToken);
                        continue;

                    default:
                        Log.Warn($"[McpServer][InProcess] Received unrecognizable message, responding with error.");
                        await _session.SendMessageAsync(new JsonRpcResponse
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
            Log.Error($"[McpServer][InProcess] Error in transport loop.", ex);
            _transportPair.CompleteServer(ex);
            return;
        }
        finally
        {
            _transportPair.CompleteServer();
            await _session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task HandleNotificationAsync(JsonRpcNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await _manager.HandleRequestAsync(
                new JsonRpcRequest { Method = notification.Method, Params = notification.Params },
                services => services.AddTransportSession(_session, Log),
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

    private async Task HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _manager.HandleRequestAsync(
                request,
                services => services.AddTransportSession(_session, Log),
                cancellationToken).ConfigureAwait(false);
            if (response is not null)
            {
                await _session.SendMessageAsync(response, cancellationToken).ConfigureAwait(false);
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
                await _session.SendMessageAsync(new JsonRpcResponse
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
}