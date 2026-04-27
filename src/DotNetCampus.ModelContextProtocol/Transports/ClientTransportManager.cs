using System.Collections.Concurrent;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Utils;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// 用于管理 MCP 客户端传输层的管理器。
/// </summary>
internal class ClientTransportManager(IClientTransportContext context) : IClientTransportManager, IMcpTransportLogger
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = [];
    private IClientTransport? _transport;
    private Func<CreateMessageRequestParams, CancellationToken, Task<CreateMessageResult>>? _samplingHandler;

    /// <inheritdoc />
    public IClientTransportContext Context { get; } = context;

    /// <inheritdoc />
    public IMcpLogger Logger => Context.Logger;

    /// <inheritdoc />
    public McpTransportRawMessageLoggingDetailLevel RawMessageLoggingDetailLevel { get; init; }

    /// <summary>
    /// 设置传输层实例。
    /// </summary>
    internal void SetTransport(IClientTransport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// 设置 Sampling 请求处理器，供服务器主动发起 sampling/createMessage 请求时调用。
    /// </summary>
    internal void SetSamplingHandler(Func<CreateMessageRequestParams, CancellationToken, Task<CreateMessageResult>> handler)
    {
        _samplingHandler = handler;
    }

    /// <inheritdoc />
    public RequestId MakeNewRequestId()
    {
        return RequestId.MakeNew();
    }

    /// <inheritdoc />
    public ValueTask<JsonRpcMessage?> ReadMessageAsync(string messageLine)
    {
        var message = JsonElement.Parse(messageLine);
        return ValueTask.FromResult(ClassifyAndDeserialize(message));
    }

    /// <summary>
    /// 根据 JSON-RPC 2.0 字段特征将 <paramref name="element"/> 分类并反序列化为具体消息类型。
    /// </summary>
    private static JsonRpcMessage? ClassifyAndDeserialize(JsonElement element)
    {
        if (element.TryGetProperty("method", out _))
        {
            return element.Deserialize(McpInternalJsonContext.Default.JsonRpcRequest);
        }

        if (element.TryGetProperty("result", out _) || element.TryGetProperty("error", out _))
        {
            return element.Deserialize(McpInternalJsonContext.Default.JsonRpcResponse);
        }

        return null;
    }

    /// <inheritdoc />
    public ValueTask<JsonRpcResponse?> ReadResponseAsync(string responseLine)
    {
        var message = JsonSerializer.Deserialize(responseLine, McpInternalJsonContext.Default.JsonRpcResponse);
        return ValueTask.FromResult<JsonRpcResponse?>(message);
    }

    /// <inheritdoc />
    public ValueTask<JsonRpcResponse?> ReadResponseAsync(Stream responseStream)
    {
        var message = JsonSerializer.Deserialize(responseStream, McpInternalJsonContext.Default.JsonRpcResponse);
        return ValueTask.FromResult<JsonRpcResponse?>(message);
    }

    /// <inheritdoc />
    public string WriteMessageAsync(JsonRpcMessage message) => message switch
    {
        JsonRpcRequest request => JsonSerializer.Serialize(request, McpInternalJsonContext.Default.JsonRpcRequest),
        JsonRpcResponse response => JsonSerializer.Serialize(response, McpInternalJsonContext.Default.JsonRpcResponse),
        JsonRpcNotification notification => JsonSerializer.Serialize(notification, McpInternalJsonContext.Default.JsonRpcNotification),
        _ => throw new ArgumentException($"不支持的消息类型：{message.GetType().FullName}."),
    };

    /// <inheritdoc />
    public async ValueTask WriteMessageAsync(Stream requestStream, JsonRpcMessage message, CancellationToken cancellationToken)
    {
        await (message switch
        {
            JsonRpcRequest request => JsonSerializer.SerializeAsync(
                requestStream, request, McpInternalJsonContext.Default.JsonRpcRequest, cancellationToken),
            JsonRpcResponse response => JsonSerializer.SerializeAsync(
                requestStream, response, McpInternalJsonContext.Default.JsonRpcResponse, cancellationToken),
            JsonRpcNotification notification => JsonSerializer.SerializeAsync(
                requestStream, notification, McpInternalJsonContext.Default.JsonRpcNotification, cancellationToken),
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
            Context.Logger.Debug($"[McpClient][Mcp] Response matched to pending request. Id={id}");
            tcs.SetResult(response);
        }
        else
        {
            Context.Logger.Warn($"[McpClient][Mcp] Received unmatched response. Id={id}");
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask HandleServerRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Id is null)
        {
            // JSON-RPC 2.0 规定：通知（notification）没有 id，不应发送响应。
            return;
        }

        Context.Logger.Info($"[McpClient][Mcp] Received server-initiated request. Method={request.Method}, Id={request.Id}");

        JsonRpcResponse response;

        if (request.Method == RequestMethods.SamplingCreateMessage && _samplingHandler is { } handler)
        {
            try
            {
                CreateMessageRequestParams? requestParams = null;
                if (request.Params is { } paramsElement)
                {
                    requestParams = paramsElement.Deserialize(McpInternalJsonContext.Default.CreateMessageRequestParams);
                }
                requestParams ??= new CreateMessageRequestParams { Messages = [], MaxTokens = 1024 };

                var result = await handler(requestParams, cancellationToken).ConfigureAwait(false);
                Context.Logger.Debug($"[McpClient][Mcp] Sampling request handled successfully. Id={request.Id}");
                response = new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = JsonSerializer.SerializeToElement(result, McpInternalJsonContext.Default.CreateMessageResult),
                };
            }
            catch (Exception ex)
            {
                Context.Logger.Error($"[McpClient][Mcp] Sampling request handler threw exception. Id={request.Id}, Error={ex.Message}");
                response = new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = (int)JsonRpcErrorCode.InternalError,
                        Message = ex.Message,
                    },
                };
            }
        }
        else
        {
            Context.Logger.Warn($"[McpClient][Mcp] Unsupported server-initiated request method. Method={request.Method}, Id={request.Id}");
            response = new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = (int)JsonRpcErrorCode.MethodNotFound,
                    Message = $"Method '{request.Method}' not found or no handler registered.",
                },
            };
        }

        await SendMessageAsync(response, cancellationToken).ConfigureAwait(false);
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
        if (!_pendingRequests.TryAdd(id, tcs))
        {
            throw new InvalidOperationException($"已存在相同 ID 的挂起请求：{id}。");
        }

        using var registration = cancellationToken.Register(() =>
        {
            if (_pendingRequests.TryRemove(id, out var removed))
            {
                removed.TrySetCanceled(cancellationToken);
            }
        });

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

    /// <summary>
    /// 连接到 MCP 服务器，然后发送 MCP 的 <see cref="RequestMethods.Initialize"/> 请求进行初始化。
    /// </summary>
    /// <param name="client"><see cref="McpClient"/> 的实例。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已连接成功服务器，发送完初始化请求，收到初始化响应，然后发送完初始化完成通知后，再返回。</returns>
    public async ValueTask<InitializeResult> ConnectAndInitializeAsync(McpClient client, CancellationToken cancellationToken = default)
    {
        if (_transport is null)
        {
            throw new InvalidOperationException("传输层未初始化");
        }

        await _transport.ConnectAsync(cancellationToken);

        // 发送 initialize 请求。
        var request = new JsonRpcRequest
        {
            Id = MakeNewRequestId().ToJsonElement(),
            Method = RequestMethods.Initialize,
            Params = JsonSerializer.SerializeToElement(new InitializeRequestParams
            {
                ProtocolVersion = client.PreferredProtocolVersion,
                ClientInfo = new Implementation
                {
                    Name = client.ClientName,
                    Version = client.ClientVersion,
                },
                Capabilities = client.Capabilities,
            }, McpInternalJsonContext.Default.InitializeRequestParams),
        };

        var response = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.Error is not null)
        {
            throw new McpClientException($"初始化失败: {response.Error.Message}");
        }

        if (response.Result is not { } responseResult)
        {
            throw new McpClientException("初始化响应格式不正确");
        }

        var result = responseResult.Deserialize<InitializeResult>(McpInternalJsonContext.Default.InitializeResult)
                     ?? throw new McpClientException("无法解析初始化响应");

        if (!client.SupportedProtocolVersions.Contains(result.ProtocolVersion, StringComparer.Ordinal))
        {
            throw new McpClientException($"服务器返回了客户端不支持的协议版本：{result.ProtocolVersion}");
        }

        if (!string.Equals(result.ProtocolVersion, client.PreferredProtocolVersion, StringComparison.Ordinal))
        {
            Context.Logger.Info(
                $"[McpClient][Mcp] Protocol version negotiated. Requested={client.PreferredProtocolVersion}, Negotiated={result.ProtocolVersion}");
        }

        // 发送 initialized 通知。
        await SendNotificationAsync(new JsonRpcNotification
        {
            Method = RequestMethods.NotificationsInitialized,
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_transport is null)
        {
            throw new InvalidOperationException("传输层未初始化");
        }

        await _transport.DisconnectAsync(cancellationToken);
    }

    /// <summary>
    /// 发送 JSON-RPC 消息（由具体传输层实现调用）。
    /// </summary>
    private ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        if (_transport is null)
        {
            throw new InvalidOperationException("传输层未初始化");
        }

        return _transport.SendMessageAsync(message, cancellationToken);
    }
}
