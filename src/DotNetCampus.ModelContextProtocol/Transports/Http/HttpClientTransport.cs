using System.Text;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// HTTP 客户端传输层，用于通过 HTTP 进行 MCP 通信。
/// </summary>
public class HttpClientTransport : IClientTransport
{
    private readonly IClientTransportManager _manager;
    private readonly HttpClientTransportOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _isExternalHttpClient;

    /// <summary>
    /// 当前会话 ID。在初始化请求后由服务器分配。
    /// </summary>
    private string? _sessionId;

    // SSE 后台任务相关
    private CancellationTokenSource? _sseCts;
    private Task? _sseLoopTask;

    /// <summary>
    /// 初始化 <see cref="HttpClientTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="options">HTTP 传输层配置选项。</param>
    public HttpClientTransport(IClientTransportManager manager, HttpClientTransportOptions options)
    {
        _manager = manager;
        _options = options;

        if (options.HttpClient is { } httpClient)
        {
            _httpClient = httpClient;
            _isExternalHttpClient = true;
        }
        else
        {
            _httpClient = new HttpClient();
            _isExternalHttpClient = false;
        }
    }

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        // 按照 MCP HTTP 传输层规范，连接的建立实际上是在发送 Initialize 请求时完成的 (Handshake)。
        // ConnectAsync 在此仅作为占位符，或者用于基本的配置检查。
        Log.Info($"[McpClient][Http] Transfer client prepared for {_options.ServerUrl}");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        // 1. 停止 SSE 监听
        if (_sseCts != null)
        {
            _sseCts.Cancel();
            try
            {
                if (_sseLoopTask != null)
                {
                    await _sseLoopTask;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Warn($"[McpClient][Http] SSE loop error during disconnect: {ex.Message}");
            }
            _sseCts.Dispose();
            _sseCts = null;
            _sseLoopTask = null;
        }

        if (_sessionId is null)
        {
            return;
        }

        var sessionId = _sessionId;
        _sessionId = null;

        // 2. 发送 DELETE 请求终止会话
        try
        {
            Log.Debug($"[McpClient][Http][Mcp:{sessionId}] Sending DELETE request to close session");

            using var request = new HttpRequestMessage(HttpMethod.Delete, _options.ServerUrl);
            request.Headers.Add("Mcp-Session-Id", sessionId);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            // 即便失败也视为断开，因为我们已经丢弃了 sessionId
            if (!response.IsSuccessStatusCode)
            {
                Log.Debug($"[McpClient][Http][Mcp:{sessionId}] Server returned {response.StatusCode} on DELETE");
            }

            Log.Info($"[McpClient][Http][Mcp:{sessionId}] Session closed locally");
        }
        catch (Exception ex)
        {
            Log.Warn($"[McpClient][Http][Mcp:{sessionId}] Failed to close session gracefully", ex);
        }
    }

    /// <inheritdoc />
    public ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken) => message switch
    {
        JsonRpcRequest request => SendRequestAsync(request, cancellationToken),
        JsonRpcNotification notification => SendNotificationAsync(notification, cancellationToken),
        _ => throw new ArgumentException($"不支持的消息类型：{message.GetType().FullName}."),
    };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();

        if (!_isExternalHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async ValueTask SendRequestAsync(JsonRpcRequest message, CancellationToken cancellationToken)
    {
        var isInitialize = message.Method == "initialize";
        var currentSessionId = _sessionId;

        if (!isInitialize && currentSessionId is null)
        {
            Log.Warn($"[McpClient][Http] Cannot send message: not initialized");
            throw new InvalidOperationException("Not initialized. Call initialize first.");
        }

        Log.Debug($"[McpClient][Http][Mcp:{currentSessionId ?? "new"}][{message.Method}][Request] Sending message[{message.Id}]");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.ServerUrl);

            // 1. 设置 Header
            if (!isInitialize && currentSessionId is not null)
            {
                request.Headers.Add("Mcp-Session-Id", currentSessionId);
            }
            // 客户端必须声明支持 SSE，以便服务器可以升级连接
            request.Headers.Add("Accept", "application/json, text/event-stream");
            // 声明协议版本
            request.Headers.Add("MCP-Protocol-Version", ProtocolVersion.Current);

            // 2. 序列化并发送
            // 注意：序列化由 Manager 处理，确保 AOT 兼容
            var jsonContent = _manager.WriteMessageAsync(message);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // 发送请求（不使用 Dispose Response，因为我们可能需要保留 Stream）
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // 3. 处理握手 (Initialize) 的会话 ID
            if (isInitialize)
            {
                if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessionIds))
                {
                    _sessionId = sessionIds.FirstOrDefault();
                    Log.Debug($"[McpClient][Http][Mcp:{_sessionId}] Received session ID from server");
                }

                if (string.IsNullOrEmpty(_sessionId))
                {
                    Log.Warn($"[McpClient][Http] Server did not return Mcp-Session-Id on initialize");
                    // 某些实现允许旧协议通过 Body 返回，但这里我们严格遵循新规范
                }
            }

            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType;

            // 4. 处理响应内容
            if (contentType?.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase) is true)
            {
                // 情况 A: 服务端升级了连接为 SSE (通常仅在 Initialize 时发生)
                Log.Debug($"[McpClient][Http][Mcp:{_sessionId}][{message.Method}] Connection upgraded to SSE stream");

                // 接管 Response Stream
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                // 在升级模式下，第一个 SSE 事件必须是 Initialize 的响应
                // 我们读取它，然后启动后台循环
                await HandleSseUpgradeAsync(stream, message.Id, cancellationToken);
            }
            else
            {
                // 情况 B: 普通 JSON 响应
                using (response)
                {
                    var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

                    // 读取并处理响应（由 Manager 反序列化）
                    var jsonRpcResponse = await _manager.ReadResponseAsync(responseStream);

                    if (jsonRpcResponse != null)
                    {
                        Log.Debug($"[McpClient][Http][Mcp:{_sessionId}][{message.Method}][Response] Received response[{jsonRpcResponse.Id}] directly via POST");
                        // 手动注入响应到处理管道
                        await _manager.HandleRespondAsync(jsonRpcResponse, cancellationToken);
                    }
                }

                // 如果这是 Initialize 请求且我们还没有启动 SSE 监听，现在启动
                if (isInitialize && _sseLoopTask == null && _sessionId != null)
                {
                    StartSseLoop(_sessionId);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[McpClient][Http][Mcp:{_sessionId}][{message.Method}] Failed to send request", ex);
            throw;
        }
    }

    private async Task HandleSseUpgradeAsync(Stream stream, IJsonRpcMessageId? expectedRequestId, CancellationToken cancellationToken)
    {
        // 这是一个复杂的场景：我们必须从 Stream 中读取第一个消息作为当前 Request 的响应，
        // 然后将 Stream 的剩余部分交给后台任务。
        // 由于 Stream不可 克隆，我们必须由同一个 Reader 处理。

        // 这里的策略是：直接启动 SSE Loop，但在 Loop 内部有一个机制来捕获第一个消息的完成，
        // 或者我们简单地让 Loop 运行，它会自动分发消息。
        // 关键点：SendRequestAsync 需要等待直到 Response 被处理吗？
        // ClientTransport 的 SendMessageAsync 是 fire-and-forget (ValueTask)，
        // 或者是等待发送完成？通常是等待“发送”完成。响应是异步回来的。
        // 所以，针对 HttpClientTransport，我们只要确保 SSE Loop 开始读取即可。
        // 当 Loop 读到第一个消息并调用 _manager.HandleRespondAsync 时，McpClient 层会匹配 ID 并完成 Task。

        // 所以我们只需要把这个 Stream 包装进 SSE Loop 即可。
        StartSseLoop(_sessionId!, stream);

        // 为了确保 Connected 状态一致性，我们在这里不做额外的 Await，
        // 只要 Loop 开始运行，它很快就会读到第一个包。
    }

    private async ValueTask SendNotificationAsync(JsonRpcNotification message, CancellationToken cancellationToken)
    {
        var currentSessionId = _sessionId;
        if (currentSessionId is null)
        {
            throw new InvalidOperationException("Not initialized.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.ServerUrl);
            request.Headers.Add("Mcp-Session-Id", currentSessionId);
            request.Headers.Add("Accept", "application/json");

            var jsonContent = _manager.WriteMessageAsync(message);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            // 202 Accepted 是正常的通知响应，200 OK 也可以
            if (!response.IsSuccessStatusCode)
            {
                Log.Warn($"[McpClient][Http][Mcp:{currentSessionId}] Notification sent but server returned {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[McpClient][Http] Failed to send notification", ex);
            throw;
        }
    }

    private void StartSseLoop(string sessionId, Stream? existingStream = null)
    {
        if (_sseLoopTask != null)
        {
            return;
        }

        _sseCts = new CancellationTokenSource();
        _sseLoopTask = Task.Run(() => SseLoopAsync(sessionId, existingStream, _sseCts.Token));
    }

    private async Task SseLoopAsync(string sessionId, Stream? initialStream, CancellationToken cancellationToken)
    {
        Log.Info($"[McpClient][Http][Mcp:{sessionId}] Starting SSE message loop");

        // 如果只有 initialStream，我们需要在它结束后（如果它是被关闭了）考虑是否重连？
        // 通常 initialStream (来自 Initialize) 如果断开，就意味着连接需要重连。
        // 如果没有 initialStream，我们需要发起 GET 请求。

        Stream? currentStream = initialStream;
        HttpResponseMessage? currentResponse = null; // 用于保持引用以免 Stream 被 Dispose

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (currentStream == null)
                    {
                        // 发起 GET /mcp 连接
                        var request = new HttpRequestMessage(HttpMethod.Get, _options.ServerUrl);
                        request.Headers.Add("Mcp-Session-Id", sessionId);
                        request.Headers.Add("Accept", "text/event-stream");

                        // 长时间运行的请求
                        currentResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        currentResponse.EnsureSuccessStatusCode();
                        currentStream = await currentResponse.Content.ReadAsStreamAsync(cancellationToken);
                        Log.Debug($"[McpClient][Http][Mcp:{sessionId}] SSE Connected");
                    }

                    using (var reader = new StreamReader(currentStream, Encoding.UTF8, leaveOpen: false))
                    {
                        string? line;
                        string? eventType = null;
                        var dataBuilder = new StringBuilder();

                        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                        {
                            if (string.IsNullOrEmpty(line))
                            {
                                // End of event
                                if (dataBuilder.Length > 0)
                                {
                                    var data = dataBuilder.ToString();
                                    dataBuilder.Clear();
                                    await ProcessSseEventAsync(sessionId, eventType, data, cancellationToken);
                                }
                                eventType = null;
                                continue;
                            }

                            if (line.StartsWith("event:", StringComparison.Ordinal))
                            {
                                eventType = line.Substring(6).Trim();
                            }
                            else if (line.StartsWith("data:", StringComparison.Ordinal))
                            {
                                dataBuilder.Append(line.Substring(5).Trim());
                            }
                            // Ignore id, retry, and comments
                        }
                    }

                    // Stream ended
                    Log.Info($"[McpClient][Http][Mcp:{sessionId}] SSE Stream ended by server");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Warn($"[McpClient][Http][Mcp:{sessionId}] SSE Loop error: {ex.Message}. Reconnecting in 1s...");
                    await Task.Delay(1000, cancellationToken);
                }
                finally
                {
                    currentResponse?.Dispose();
                    currentResponse = null;
                    currentStream = null;
                }
            }
        }
        finally
        {
            Log.Info($"[McpClient][Http][Mcp:{sessionId}] SSE Loop stopped");
        }
    }

    private async ValueTask ProcessSseEventAsync(string sessionId, string? eventType, string data, CancellationToken token)
    {
        if (eventType == "endpoint") return; // Ignore legacy

        // Default event is message
        if (string.IsNullOrEmpty(eventType) || eventType == "message")
        {
            try
            {
                // 反序列化由 Manager 处理
                var response = await _manager.ReadResponseAsync(data);
                if (response != null)
                {
                    // 只有 Response 目前被支持 (Client 侧)
                    await _manager.HandleRespondAsync(response, token);
                }
                else
                {
                    // 可能是 Request/Notification (Server->Client)，暂不支持
                    Log.Debug($"[McpClient][Http][Mcp:{sessionId}] Received non-response message via SSE");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[McpClient][Http][Mcp:{sessionId}] Error processing SSE message", ex);
            }
        }
    }
}
