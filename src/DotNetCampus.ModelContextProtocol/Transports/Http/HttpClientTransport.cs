using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
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
    private readonly IMcpLogger _logger;

    // 会话状态
    private string? _sessionId;
    private string? _protocolVersion;

    // 后台接收循环 (GET Loop)
    private Task? _receiveLoopTask;
    private CancellationTokenSource? _receiveLoopCts;

#if NET9_0_OR_GREATER
    private readonly Lock _loopLock = new();
#else
    private readonly object _loopLock = new();
#endif

    /// <summary>
    /// 初始化 <see cref="HttpClientTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="options">HTTP 传输层配置选项。</param>
    public HttpClientTransport(IClientTransportManager manager, HttpClientTransportOptions options)
    {
        _manager = manager;
        _options = options;
        _logger = manager.Context.Logger;

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

    /// <inheritdoc />
    public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        // Streamable HTTP 是惰性连接，真正连接发生在第一次发送消息时
        // 但我们在概念上认为此时已就绪
        _logger.Info($"[McpClient][Http] Transport connected (stateless/lazy)");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.Info($"[McpClient][Http] Disconnecting transport...");

        // 1. 停止后台接收循环
        StopReceiveLoop();

        // 2. 发送 DELETE 请求终止会话 (Best Effort)
        if (!string.IsNullOrEmpty(_sessionId))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Delete, _options.ServerUrl);
                request.Headers.Add("Mcp-Session-Id", _sessionId);

                // 设置较短的超时，避免长时间卡住断开流程
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var response = await _httpClient.SendAsync(request, cts.Token);

                _logger.Debug($"[McpClient][Http] Session {_sessionId} terminated with status {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[McpClient][Http] Failed to strictly terminate session {_sessionId}: {ex.Message}");
            }
        }

        _sessionId = null;
        _protocolVersion = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();

        if (!_isExternalHttpClient)
        {
            _httpClient.Dispose();
        }

        _receiveLoopCts?.Dispose();
    }

    /// <inheritdoc />
    public ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        // 将所有消息类型统一包装为 POST 请求
        return SendRequestCoreAsync(message, cancellationToken);
    }

    private async ValueTask SendRequestCoreAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var isInitialize = message is JsonRpcRequest { Method: "initialize" };
            var requestUrl = _options.ServerUrl;

            // 1. 构建请求
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

            // 2. 设置标准头
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            if (!string.IsNullOrEmpty(_sessionId))
            {
                request.Headers.Add("Mcp-Session-Id", _sessionId);
            }

            if (!string.IsNullOrEmpty(_protocolVersion))
            {
                request.Headers.Add("Mcp-Protocol-Version", _protocolVersion);
            }

            // 3. 序列化内容
            var jsonContent = _manager.WriteMessageAsync(message);
            // 这里为了避免 "application/json; charset=utf-8" 可能导致的兼容性问题，手动构造 ContentType
            var content = new StringContent(jsonContent, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;

            _logger.Debug($"[McpClient][Http][POST] Sending {(isInitialize ? "Initialize" : message.GetType().Name)} to {requestUrl}");

            // 4. 发送请求 (ResponseHeadersRead 以支持流式响应)
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // 5. 检查握手响应 (Initialize) 提取 SessionId
            if (isInitialize && response.Headers.TryGetValues("Mcp-Session-Id", out var headers))
            {
                var newId = headers.FirstOrDefault();
                if (!string.IsNullOrEmpty(newId) && _sessionId != newId)
                {
                    _sessionId = newId;
                    _logger.Info($"[McpClient][Http] Session negotiated: {_sessionId}");

                    // 握手成功，启动后台接收循环
                    StartReceiveLoop();
                }
            }

            // 6. 处理响应
            response.EnsureSuccessStatusCode();

            var mediaType = response.Content.Headers.ContentType?.MediaType;

            if (string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
            {
                // Case A: 瞬态 SSE 流 (Transient SSE)
                _logger.Debug($"[McpClient][Http] Received Transient SSE Stream response");

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await ProcessSseStreamAsync(stream, cancellationToken);
            }
            else
            {
                // Case B: 标准 JSON 响应
                if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    _logger.Debug($"[McpClient][Http] Received 202 Accepted (Response pending via GET loop)");
                    return;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                // 将响应流反序列化并分发
                var rpcResponse = await _manager.ReadResponseAsync(stream);
                if (rpcResponse != null)
                {
                    // Initialize Response: Try capture ProtocolVersion
                    if (isInitialize && rpcResponse.Result is { ValueKind: JsonValueKind.Object } resultElement)
                    {
                        if (resultElement.TryGetProperty("protocolVersion", out var pv) && pv.ValueKind == JsonValueKind.String)
                        {
                            _protocolVersion = pv.GetString();
                        }
                    }

                    await _manager.HandleRespondAsync(rpcResponse, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[McpClient][Http] Error sending message: {ex.Message}", ex);
            throw;
        }
    }

    // --- 后台接收循环逻辑 (GET Loop) ---

    private void StartReceiveLoop()
    {
        lock (_loopLock)
        {
            if (_receiveLoopTask is { IsCompleted: false })
            {
                return;
            }

            _receiveLoopCts = new CancellationTokenSource();
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_receiveLoopCts.Token));
        }
    }

    private void StopReceiveLoop()
    {
        lock (_loopLock)
        {
            _receiveLoopCts?.Cancel();
            _receiveLoopTask = null;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        _logger.Info($"[McpClient][Http] GET SSE Loop started");

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (string.IsNullOrEmpty(_sessionId))
                {
                    await Task.Delay(100, token);
                    continue;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, _options.ServerUrl);
                request.Headers.Add("Mcp-Session-Id", _sessionId);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                }
                catch (Exception sendEx)
                {
                    _logger.Warn($"[McpClient][Http] GET Loop connect error: {sendEx.Message}. Retrying...");
                    await Task.Delay(2000, token);
                    continue;
                }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.Warn($"[McpClient][Http] GET Loop received status {response.StatusCode}. Retrying...");
                        await Task.Delay(2000, token);
                        continue;
                    }

                    await using var stream = await response.Content.ReadAsStreamAsync(token);
                    await ProcessSseStreamAsync(stream, token);
                }
                _logger.Info($"[McpClient][Http] GET Loop stream ended. Reconnecting...");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[McpClient][Http] GET Loop error: {ex.Message}. Reconnecting in 2s...");
                try
                {
                    await Task.Delay(2000, token);
                }
                catch
                {
                    // 忽略此时的取消异常，确保循环能正常退出
                }
            }
        }

        _logger.Info($"[McpClient][Http] GET SSE Loop stopped");
    }

    // --- SSE 解析核心逻辑 ---

    private async Task ProcessSseStreamAsync(Stream stream, CancellationToken token)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        string? currentEvent = null;
        var dataBuffer = new StringBuilder();

        while (!token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(token);
            if (line == null)
            {
                // End of Stream
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (dataBuffer.Length > 0)
                {
                    await DispatchSseEventAsync(currentEvent, dataBuffer.ToString(), token);
                    dataBuffer.Clear();
                    currentEvent = null;
                }
                continue;
            }

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                currentEvent = line.Substring(6).Trim();
            }
            else if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                if (dataBuffer.Length > 0 && !line.StartsWith("data: ", StringComparison.Ordinal))
                {
                }
                dataBuffer.Append(line.Substring(5).Trim());
            }
        }
    }

    private async Task DispatchSseEventAsync(string? eventName, string data, CancellationToken token)
    {
        if (string.IsNullOrEmpty(data) || data == "[DONE]") return;

        if (string.IsNullOrEmpty(eventName) || string.Equals(eventName, "message", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var response = await _manager.ReadResponseAsync(data);
                if (response != null)
                {
                    await _manager.HandleRespondAsync(response, token);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"[McpClient][Http][SSE] Failed to process message: {ex.Message}");
            }
        }
    }
}
