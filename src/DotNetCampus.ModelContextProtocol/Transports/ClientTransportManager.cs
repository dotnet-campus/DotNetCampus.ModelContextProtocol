using System.Collections.Concurrent;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Utils;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// 用于管理 MCP 客户端传输层的管理器。
/// </summary>
internal class ClientTransportManager(IClientTransportContext context) : IClientTransportManager
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = [];
    private IClientTransport? _transport;

    /// <inheritdoc />
    public IClientTransportContext Context { get; } = context;

    /// <summary>
    /// 设置传输层实例。
    /// </summary>
    internal void SetTransport(IClientTransport transport)
    {
        _transport = transport;
    }

    /// <inheritdoc />
    public RequestId MakeNewRequestId()
    {
        return RequestId.MakeNew();
    }

    /// <inheritdoc />
    public ValueTask<JsonRpcResponse?> ReadResponseAsync(string responseLine)
    {
        var message = JsonSerializer.Deserialize(responseLine, McpServerResponseJsonContext.Default.JsonRpcResponse);
        return ValueTask.FromResult<JsonRpcResponse?>(message);
    }

    /// <inheritdoc />
    public ValueTask<JsonRpcResponse?> ReadResponseAsync(Stream responseStream)
    {
        var message = JsonSerializer.Deserialize(responseStream, McpServerResponseJsonContext.Default.JsonRpcResponse);
        return ValueTask.FromResult<JsonRpcResponse?>(message);
    }

    /// <inheritdoc />
    public string WriteRequestAsync(JsonRpcMessage message)
    {
        return message switch
        {
            JsonRpcRequest request => JsonSerializer.Serialize(request, McpServerRequestJsonContext.Default.JsonRpcRequest),
            JsonRpcNotification notification => JsonSerializer.Serialize(notification, McpServerRequestJsonContext.Default.JsonRpcNotification),
            _ => throw new ArgumentException($"不支持的消息类型：{message.GetType().FullName}."),
        };
    }

    /// <inheritdoc />
    public async ValueTask WriteRequestAsync(Stream requestStream, JsonRpcMessage message, CancellationToken cancellationToken)
    {
        await (message switch
        {
            JsonRpcRequest request => JsonSerializer.SerializeAsync(
                requestStream, request, McpServerRequestJsonContext.Default.JsonRpcRequest, cancellationToken),
            JsonRpcNotification notification => JsonSerializer.SerializeAsync(
                requestStream, notification, McpServerRequestJsonContext.Default.JsonRpcNotification, cancellationToken),
            _ => throw new ArgumentException($"不支持的消息类型：{message.GetType().FullName}."),
        });
    }

    /// <inheritdoc />
    public ValueTask HandleRespondAsync(JsonRpcResponse response, CancellationToken cancellationToken = default)
    {
        if (response.Id?.ToString() is not { } id)
        {
            // 直接 ToString 可能会让数字和字符串 Id 含义出现冲突（如导致数字 `1` 与字符串 `“1”` 含义相同）。
            // 但考虑到 Id 是本库生成的，所以能保证不会出现上述情况。
            return ValueTask.CompletedTask;
        }

        if (_pendingRequests.TryRemove(id, out var tcs))
        {
            tcs.SetResult(response);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 发送请求并等待响应。
    /// </summary>
    /// <param name="request">要发送的 JSON-RPC 请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>服务器的响应。</returns>
    public async ValueTask<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        if (_transport is null)
        {
            throw new InvalidOperationException("传输层未初始化");
        }

        if (request.Id?.ToString() is not { } id)
        {
            throw new InvalidOperationException("请求 ID 不能为 null");
        }

        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        try
        {
            await SendMessageAsync(request, cancellationToken).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pendingRequests.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// 发送通知（不期望响应）。
    /// </summary>
    /// <param name="notification">要发送的 JSON-RPC 通知。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public ValueTask SendNotificationAsync(JsonRpcNotification notification, CancellationToken cancellationToken = default)
    {
        return SendMessageAsync(notification, cancellationToken);
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
    /// 发送 JSON-RPC 消息（由具体传输层实现调用）。
    /// </summary>
    protected virtual ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        if (_transport is null)
        {
            throw new InvalidOperationException("传输层未初始化");
        }

        return _transport.SendMessageAsync(message, cancellationToken);
    }
}
