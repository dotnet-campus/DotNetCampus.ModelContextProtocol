using System.Collections.Concurrent;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// 用于管理 MCP 客户端传输层的管理器。
/// </summary>
internal class ClientTransportManager : IClientTransportManager
{
    private readonly ConcurrentDictionary<object, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = [];
    private IClientTransport? _transport;

    public ClientTransportManager(IClientTransportContext context)
    {
        Context = context;
    }

    /// <inheritdoc />
    public IClientTransportContext Context { get; }

    /// <inheritdoc />
    public bool IsConnected => _transport?.IsConnected ?? false;

    /// <summary>
    /// 设置传输层实例。
    /// </summary>
    internal void SetTransport(IClientTransport transport)
    {
        _transport = transport;
    }

    /// <inheritdoc />
    public async ValueTask<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        if (_transport is null)
        {
            throw new InvalidOperationException("传输层未初始化");
        }

        if (request.Id is null)
        {
            throw new InvalidOperationException("请求 ID 不能为 null");
        }

        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[request.Id] = tcs;

        try
        {
            await SendJsonRpcMessageAsync(request, cancellationToken).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            if (request.Id is not null)
            {
                _pendingRequests.TryRemove(request.Id, out _);
            }
        }
    }

    /// <inheritdoc />
    public ValueTask SendNotificationAsync(JsonRpcNotification notification, CancellationToken cancellationToken = default)
    {
        return SendJsonRpcMessageAsync(notification, cancellationToken);
    }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_transport is null)
        {
            throw new InvalidOperationException("传输层未初始化");
        }

        return _transport.ConnectAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_transport is null)
        {
            throw new InvalidOperationException("传输层未初始化");
        }

        return _transport.DisconnectAsync(cancellationToken);
    }

    /// <summary>
    /// 处理从服务器接收到的响应。
    /// </summary>
    internal void HandleResponse(JsonRpcResponse response)
    {
        if (response.Id is not null && _pendingRequests.TryRemove(response.Id, out var tcs))
        {
            tcs.SetResult(response);
        }
    }

    /// <summary>
    /// 发送 JSON-RPC 消息（由具体传输层实现调用）。
    /// </summary>
    protected virtual ValueTask SendJsonRpcMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        // 这个方法将由具体的传输层实现来覆盖
        throw new NotImplementedException("具体的传输层必须实现消息发送逻辑");
    }
}
