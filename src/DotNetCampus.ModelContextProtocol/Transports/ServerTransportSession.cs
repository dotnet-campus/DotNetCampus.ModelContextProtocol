using System.Collections.Concurrent;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// <see cref="IServerTransportSession"/> 的抽象基类，封装了通用的"等待客户端响应"模式（TCS 字典 + CancellationToken 注册）。
/// <para>
/// 各传输层的 Session 继承本类，并实现 <see cref="SendRequestMessageAsync"/> 来完成各自的"发送"操作
/// （如 Stdio 写 stdout、Streamable HTTP 写 SSE 流）。
/// </para>
/// </summary>
public abstract class ServerTransportSession : IServerTransportSession
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = [];

    /// <inheritdoc />
    public abstract string? SessionId { get; }

    /// <inheritdoc />
    public ClientCapabilities? ConnectedClientCapabilities { get; set; }

    /// <inheritdoc />
    public async Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Id?.ToString() is not { } id)
        {
            throw new InvalidOperationException("请求 ID 不能为 null。Request ID must not be null.");
        }

        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        using var registration = cancellationToken.Register(() =>
        {
            if (_pendingRequests.TryRemove(id, out var removed))
            {
                removed.TrySetCanceled(cancellationToken);
            }
        });

        try
        {
            await SendRequestMessageAsync(request, cancellationToken).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pendingRequests.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// 执行实际的消息发送操作。由子类实现，负责将 <paramref name="request"/> 写入各自的传输通道
    /// （如 Stdio 写 stdout、Streamable HTTP 写 SSE 流）。
    /// </summary>
    protected abstract Task SendRequestMessageAsync(JsonRpcRequest request, CancellationToken cancellationToken);

    /// <inheritdoc />
    public void HandleResponseAsync(JsonRpcResponse response)
    {
        if (response.Id?.ToString() is not { } id)
        {
            return;
        }

        if (_pendingRequests.TryRemove(id, out var tcs))
        {
            OnResponseReceived(id, response);
            tcs.TrySetResult(response);
        }
        else
        {
            OnUnmatchedResponse(id, response);
        }
    }

    /// <summary>
    /// 匹配的客户端响应到达时的回调（可用于日志）。默认为空实现。
    /// </summary>
    protected virtual void OnResponseReceived(string id, JsonRpcResponse response) { }

    /// <summary>
    /// 无法匹配的客户端响应到达时的回调（可用于日志）。默认为空实现。
    /// </summary>
    protected virtual void OnUnmatchedResponse(string id, JsonRpcResponse response) { }

    /// <summary>
    /// 取消所有待处理的挂起请求，供 <see cref="IAsyncDisposable.DisposeAsync"/> 调用。
    /// </summary>
    protected void CancelAllPendingRequests()
    {
        foreach (var (_, tcs) in _pendingRequests)
        {
            tcs.TrySetCanceled();
        }
        _pendingRequests.Clear();
    }

    /// <inheritdoc />
    public abstract ValueTask DisposeAsync();
}
