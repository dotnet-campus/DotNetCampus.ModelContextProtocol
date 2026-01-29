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

            request.Headers.Add("Accept", "application/json");

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

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonRpcResponse = await _manager.ParseAndCatchResponseAsync(responseContent);

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
        public async ValueTask<JsonRpcResponse?> ParseAndCatchResponseAsync(string inputMessageText)
        {
            try
            {
                return await manager.ReadResponseAsync(inputMessageText);
            }
            catch
            {
                // 响应消息格式不正确，返回 null 后，原样给 MCP 客户端报告错误。
                return null;
            }
        }
    }
}
