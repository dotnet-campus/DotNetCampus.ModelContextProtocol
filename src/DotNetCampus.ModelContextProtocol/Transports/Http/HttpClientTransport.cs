using System.Net;
using System.Text;
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

    /// <summary>
    /// 当前会话 ID。在初始化请求后由服务器分配。
    /// </summary>
    private string? _sessionId;

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
        Log.Info($"[McpClient][Http] Starting HTTP client transport to {_options.ServerUrl}");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_sessionId is null)
        {
            return;
        }

        var sessionId = _sessionId;
        _sessionId = null;

        try
        {
            Log.Debug($"[McpClient][Http][Mcp:{sessionId}] Sending DELETE request to close session");

            using var request = new HttpRequestMessage(HttpMethod.Delete, _options.ServerUrl);
            request.Headers.Add("Mcp-Session-Id", sessionId);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            Log.Info($"[McpClient][Http][Mcp:{sessionId}] Session closed successfully");
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

        Log.Debug($"[McpClient][Http][Mcp:{currentSessionId ?? "no-session"}][{message.Method}][Request] Sending message[{message.Id}]");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.ServerUrl);

            if (!isInitialize && currentSessionId is not null)
            {
                request.Headers.Add("Mcp-Session-Id", currentSessionId);
            }

            // The client MUST include an Accept header, listing both application/json and text/event-stream as supported content types.
            request.Headers.Add("Accept", "application/json, text/event-stream");

            var jsonContent = _manager.WriteRequestAsync(message);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (isInitialize && response.Headers.TryGetValues("Mcp-Session-Id", out var sessionIds))
            {
                var newSessionId = sessionIds.FirstOrDefault();
                if (!string.IsNullOrEmpty(newSessionId))
                {
                    _sessionId = newSessionId;
                    Log.Debug($"[McpClient][Http][Mcp:{_sessionId}] Received session ID from server");
                }
            }

            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            // The server MUST either return Content-Type: text/event-stream or Content-Type: application/json
            JsonRpcResponse? jsonRpcResponse;
            if (contentType?.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase) is true)
            {
                // SSE 流：可能包含多个消息（请求、通知、响应）
                Log.Debug($"[McpClient][Http][Mcp:{_sessionId}][{message.Method}] Receiving SSE stream");
                jsonRpcResponse = await _manager.ParseAndCatchSseResponseAsync(responseStream, message, _sessionId, Log, cancellationToken);
            }
            else
            {
                // 单个 JSON 响应
                jsonRpcResponse = await _manager.ParseAndCatchResponseAsync(responseStream, cancellationToken);
            }

            if (jsonRpcResponse is not null)
            {
                Log.Debug($"[McpClient][Http][Mcp:{_sessionId}][{message.Method}][Response] Received response[{jsonRpcResponse.Id}]");
                await _manager.HandleRespondAsync(jsonRpcResponse, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[McpClient][Http][Mcp:{_sessionId}][{message.Method}] Failed to send request", ex);
            throw;
        }
    }

    private async ValueTask SendNotificationAsync(JsonRpcNotification message, CancellationToken cancellationToken)
    {
        var currentSessionId = _sessionId;

        if (currentSessionId is null)
        {
            Log.Warn($"[McpClient][Http] Cannot send notification: not initialized");
            throw new InvalidOperationException("Not initialized. Call initialize first.");
        }

        Log.Debug($"[McpClient][Http][Mcp:{currentSessionId}][{message.Method}][Notification] Sending notification");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.ServerUrl);
            request.Headers.Add("Mcp-Session-Id", currentSessionId);
            request.Headers.Add("Accept", "application/json");

            var jsonContent = _manager.WriteRequestAsync(message);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                Log.Debug($"[McpClient][Http][Mcp:{currentSessionId}][{message.Method}][Notification] Notification accepted (202)");
                return;
            }

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Log.Error($"[McpClient][Http][Mcp:{currentSessionId}][{message.Method}] Failed to send notification", ex);
            throw;
        }
    }
}

file static class Extensions
{
    extension(IClientTransportManager manager)
    {
        /// <summary>
        /// 解析 application/json 格式的响应。
        /// </summary>
        public async ValueTask<JsonRpcResponse?> ParseAndCatchResponseAsync(Stream responseStream, CancellationToken cancellationToken = default)
        {
            try
            {
                return await manager.ReadResponseAsync(responseStream);
            }
            catch
            {
                // 响应消息格式不正确，返回 null 后，原样给 MCP 客户端报告错误。
                return null;
            }
        }

        /// <summary>
        /// 解析 text/event-stream 格式的响应。<br/>
        /// 根据 MCP 2025-06-18 规范：<br/>
        /// - SSE 流应该最终包含对应 POST 请求的 JSON-RPC 响应<br/>
        /// - 服务端可能在响应之前发送相关的 JSON-RPC 请求和通知<br/>
        /// - 收到响应后，服务端应该关闭 SSE 流
        /// </summary>
        public async ValueTask<JsonRpcResponse?> ParseAndCatchSseResponseAsync(Stream responseStream, JsonRpcRequest originalRequest, string? sessionId,
            IMcpLogger log, CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new StreamReader(responseStream, Encoding.UTF8);

                string? eventType = null;
                string? eventId = null;
                var dataLines = new List<string>();
                JsonRpcResponse? matchingResponse = null;

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
                {
                    // SSE 格式：空行表示事件结束
                    if (string.IsNullOrEmpty(line))
                    {
                        if (dataLines.Count > 0)
                        {
                            // 处理完整的 SSE 事件
                            var data = string.Join("\n", dataLines);
                            log.Debug(
                                $"[McpClient][Http][Mcp:{sessionId}][SSE] Received event: type={eventType ?? "message"}, id={eventId}, dataLength={data.Length}");

                            // 尝试解析为 JSON-RPC 响应
                            var response = await manager.ReadResponseAsync(data);

                            if (response is not null)
                            {
                                // 找到匹配原始请求的响应
                                if (response.Id?.Equals(originalRequest.Id) == true)
                                {
                                    log.Debug(
                                        $"[McpClient][Http][Mcp:{sessionId}][{originalRequest.Method}][Response] Received matching response[{response.Id}] via SSE");
                                    matchingResponse = response;
                                    // 根据规范，收到响应后服务端应该关闭流，我们也可以停止读取
                                    break;
                                }
                                else
                                {
                                    log.Warn(
                                        $"[McpClient][Http][Mcp:{sessionId}][SSE] Received unmatched response[{response.Id}], expected[{originalRequest.Id}]");
                                }
                            }
                            else
                            {
                                // 可能是服务端发来的请求或通知，暂时记录并忽略
                                // TODO: 未来版本可以支持处理服务端主动消息
                                log.Debug(
                                    $"[McpClient][Http][Mcp:{sessionId}][SSE] Received non-response message (possibly server request/notification), data: {data[..Math.Min(100, data.Length)]}...");
                            }

                            // 重置状态
                            eventType = null;
                            eventId = null;
                            dataLines.Clear();
                        }
                        continue;
                    }

                    // 解析 SSE 字段
                    if (line.StartsWith("event:", StringComparison.Ordinal))
                    {
                        eventType = line["event:".Length..].Trim();
                    }
                    else if (line.StartsWith("id:", StringComparison.Ordinal))
                    {
                        eventId = line["id:".Length..].Trim();
                    }
                    else if (line.StartsWith("data:", StringComparison.Ordinal))
                    {
                        dataLines.Add(line["data:".Length..].Trim());
                    }
                    else if (line.StartsWith("retry:", StringComparison.Ordinal))
                    {
                        // 暂时忽略 retry 字段
                        log.Debug($"[McpClient][Http][Mcp:{sessionId}][SSE] Received retry: {line["retry:".Length..].Trim()}");
                    }
                    // 忽略注释行（以 : 开头）
                }

                return matchingResponse;
            }
            catch (Exception ex)
            {
                log.Error($"[McpClient][Http][Mcp:{sessionId}][SSE] Failed to parse SSE response", ex);
                return null;
            }
        }
    }
}
