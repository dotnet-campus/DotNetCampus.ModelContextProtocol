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

            // 发送请求
            // 注意：我们不能立即 Dispose response，因为在 SSE 升级场景下需要移交控制权
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var handover = false;

            try
            {
                // 3. 处理握手 (Initialize) 的会话 ID
                if (isInitialize)
                {
                    if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessionIds))
                    {
                        var newSessionId = sessionIds.FirstOrDefault();
                        if (!string.IsNullOrEmpty(newSessionId))
                        {
                            _sessionId = newSessionId;
                            Log.Debug($"[McpClient][Http][Mcp:{_sessionId}] Received session ID from server");
                        }
                    }

                    if (string.IsNullOrEmpty(_sessionId))
                    {
                        Log.Warn($"[McpClient][Http] Server did not return Mcp-Session-Id on initialize");
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

                    // 在升级模式下，同步读取并处理第一条消息（即 Initialize 响应）
                    var reader = await ReadFirstSseMessageAsync(stream, cancellationToken);

                    // 将 Reader 和 Response 的所有权移交给后台循环
                    StartSseLoop(_sessionId!, reader, response);
                    handover = true;
                }
                else
                {
                    // 情况 B: 普通 JSON 响应
                    var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

                    // 读取并处理响应（由 Manager 反序列化）
                    var jsonRpcResponse = await _manager.ReadResponseAsync(responseStream);

                    if (jsonRpcResponse != null)
                    {
                        Log.Debug($"[McpClient][Http][Mcp:{_sessionId}][{message.Method}][Response] Received response[{jsonRpcResponse.Id}] directly via POST");
                        // 手动注入响应到处理管道
                        await _manager.HandleRespondAsync(jsonRpcResponse, cancellationToken);
                    }

                    // 如果这是 Initialize 请求且我们还没有启动 SSE 监听，现在启动
                    if (isInitialize && _sseLoopTask == null && _sessionId != null)
                    {
                        StartSseLoop(_sessionId!);
                    }
                }
            }
            finally
            {
                if (!handover)
                {
                    response.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[McpClient][Http][Mcp:{_sessionId}][{message.Method}] Failed to send request", ex);
            throw;
        }
    }

    private async Task<StreamReader> ReadFirstSseMessageAsync(Stream stream, CancellationToken cancellationToken)
    {
        var reader = new StreamReader(stream, Encoding.UTF8); // Default buffer size, handle BOM

        string? line;
        string? eventType = null;
        var dataBuilder = new StringBuilder();

        // 仅读取第一条完整的 SSE 消息
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrEmpty(line))
            {
                // End of event
                if (dataBuilder.Length > 0)
                {
                    var data = dataBuilder.ToString();
                    await ProcessSseEventAsync(_sessionId!, eventType, data, cancellationToken);

                    // 返回 Reader 以供后续使用
                    return reader;
                }

                // 忽略没有数据的事件（如心跳）
                eventType = null;
                dataBuilder.Clear();
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
        }

        throw new IOException("Stream closed before receiving first SSE message");
    }

    private void StartSseLoop(string sessionId, StreamReader? existingReader = null, HttpResponseMessage? existingResponse = null)
    {
        if (_sseLoopTask != null)
        {
            return;
        }

        _sseCts = new CancellationTokenSource();
        _sseLoopTask = Task.Run(() => SseLoopAsync(sessionId, existingReader, existingResponse, _sseCts.Token));
    }

    private async Task SseLoopAsync(string sessionId, StreamReader? initialReader, HttpResponseMessage? initialResponse, CancellationToken cancellationToken)
    {
        Log.Info($"[McpClient][Http][Mcp:{sessionId}] Starting SSE message loop");

        var currentReader = initialReader;
        var currentResponse = initialResponse;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (currentReader == null)
                    {
                        // 发起 GET /mcp 连接
                        var request = new HttpRequestMessage(HttpMethod.Get, _options.ServerUrl);
                        request.Headers.Add("Mcp-Session-Id", sessionId);
                        request.Headers.Add("Accept", "text/event-stream");

                        // 长时间运行的请求
                        currentResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        currentResponse.EnsureSuccessStatusCode();
                        var stream = await currentResponse.Content.ReadAsStreamAsync(cancellationToken);
                        currentReader = new StreamReader(stream, Encoding.UTF8);
                        Log.Debug($"[McpClient][Http][Mcp:{sessionId}] SSE Connected");
                    }

                    string? eventType = null;
                    var dataBuilder = new StringBuilder();

                    while (await currentReader.ReadLineAsync(cancellationToken) is { } line)
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
                    currentReader?.Dispose();
                    currentReader = null;

                    currentResponse?.Dispose();
                    currentResponse = null;
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
        if (eventType == "endpoint")
        {
            return; // Ignore legacy
        }

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
