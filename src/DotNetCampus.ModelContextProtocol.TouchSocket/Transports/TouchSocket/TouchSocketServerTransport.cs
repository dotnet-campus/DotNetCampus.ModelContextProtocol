using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Hosting.Services;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Servers;
using DotNetCampus.ModelContextProtocol.Transports;
using DotNetCampus.ModelContextProtocol.Utils;
using TouchSocket.Core;
using TouchSocket.Http;

namespace DotNetCampus.ModelContextProtocol.TouchSocket.Transports.TouchSocket;

/// <summary>
/// 基于 TouchSocket 的 HTTP 传输层实现。<br/>
/// 支持监听 0.0.0.0 等所有网络接口。
/// </summary>
public class TouchSocketServerTransport : IServerTransport
{
    private readonly IServerTransportManager _manager;
    private readonly TouchSocketServerTransportOptions _options;
    private readonly HttpService _httpService = new();
    private readonly ConcurrentDictionary<string, LegacySseSession> _legacySseSessions = [];

    /// <summary>
    /// 初始化 <see cref="TouchSocketServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="options">TouchSocket HTTP 传输层配置选项。</param>
    public TouchSocketServerTransport(IServerTransportManager manager, TouchSocketServerTransportOptions options)
    {
        _manager = manager;
        _options = options;
    }

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public async Task<Task> StartAsync(CancellationToken cancellationToken = default)
    {
        var config = new TouchSocketConfig()
            .SetListenIPHosts($"{_options.Host}:{_options.Port}");

        _httpService.PluginManager.Add(new McpHttpPlugin(this));

        await _httpService.SetupAsync(config);
        var task = _httpService.StartAsync(cancellationToken);

        Log.Info($"[McpServer][TouchSocket] listening on {_options.GetUrl()}, endpoint: {_options.EndPoint}");

        return Task.FromResult(task);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _httpService.StopAsync();
        Log.Info($"[McpServer][TouchSocket] stopped listening");
        _httpService.Dispose();
    }

    #region HTTP

    private async Task HandleRequestAsync(HttpContextEventArgs e)
    {
        var context = e.Context;
        var endpoint = context.Request.RelativeURL;

        Log.Debug($"[McpServer][TouchSocket][Http] Received request: {context.Request.Method} {endpoint}");

        // 请求安全性验证。
        var validationError = ValidateRequest(context);
        if (validationError.HasValue)
        {
            var (statusCode, message) = validationError.Value;
            Log.Warn($"[McpServer][TouchSocket][Http] Request validation failed: {statusCode} - {message}");
            context.Response.StatusCode = int.Parse(statusCode);
            context.Response.SetContent(Encoding.UTF8.GetBytes(message));
            await context.Response.AnswerAsync();
            return;
        }

        context.Response.SetCorsHeaders();

        if (context.Request.Method == "OPTIONS")
        {
            context.Response.StatusCode = 200;
            await context.Response.AnswerAsync();
            return;
        }

        var method = context.Request.Method;

        // Streamable HTTP: 客户端建立连接。
        if (method == "GET" && endpoint.Equals(_options.EndPoint, StringComparison.OrdinalIgnoreCase))
        {
            await HandleStreamableHttpConnectionAsync(context);
            return;
        }

        // Streamable HTTP: 客户端发送消息。
        if (method == "POST" && endpoint.Equals(_options.EndPoint, StringComparison.OrdinalIgnoreCase))
        {
            await HandleStreamableHttpMessageAsync(context);
            return;
        }

        // Streamable HTTP: 客户端关闭连接。
        if (method == "DELETE" && endpoint.Equals(_options.EndPoint, StringComparison.OrdinalIgnoreCase))
        {
            await HandleStreamableHttpDisconnectionAsync(context);
            return;
        }

        // 旧协议 (2024-11-05) SSE: 客户端建立连接。
        if (method == "GET" && endpoint.Equals(_options.SseEndPoint, StringComparison.OrdinalIgnoreCase))
        {
            await HandleLegacySseConnectionAsync(context);
            return;
        }

        // 旧协议 (2024-11-05) SSE: 客户端发送消息。
        if (method == "POST" && endpoint.Equals(_options.SseMessageEndPoint, StringComparison.OrdinalIgnoreCase))
        {
            await HandleLegacyMessageRequestAsync(context);
            return;
        }

        Log.Warn($"[McpServer][TouchSocket] No handler found for {method} {endpoint}");
        context.Response.StatusCode = 404;
        await context.Response.AnswerAsync();
    }

    #endregion

    #region MCP Streamable HTTP 协议

    /// <summary>
    /// Streamable HTTP: 客户端建立连接。
    /// </summary>
    private async ValueTask HandleStreamableHttpConnectionAsync(HttpContext context)
    {
        var sessionId = context.Request.Headers.Get("Mcp-Session-Id");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Log.Warn($"[McpServer][TouchSocket][Mcp:no-mcp-session-id] Connection failed due to missing Mcp-Session-Id header");
            context.Response.StatusCode = 400;
            context.Response.SetContent("Missing Mcp-Session-Id header"u8.ToArray());
            await context.Response.AnswerAsync();
            return;
        }

        Log.Info($"[McpServer][TouchSocket][Mcp:{sessionId}] Establishing connection");
        // TouchSocket SSE 连接暂不支持，返回成功即可
        context.Response.StatusCode = 200;
        await context.Response.AnswerAsync();
    }

    private async ValueTask HandleStreamableHttpMessageAsync(HttpContext context)
    {
        var bodyBytes = await context.Request.GetContentAsync();
        var message = await _manager.ReadRequestAsync(bodyBytes);
        if (message?.Method is not { } method)
        {
            Log.Warn($"[McpServer][TouchSocket][Mcp:no-mcp-session-id] Invalid JSON-RPC message received");
            context.Response.StatusCode = 400;
            context.Response.SetContent("Invalid JSON-RPC message"u8.ToArray());
            await context.Response.AnswerAsync();
            return;
        }

        var sessionId = context.Request.Headers.Get("Mcp-Session-Id");
        if (method != RequestMethods.Initialize)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                Log.Warn($"[McpServer][TouchSocket][Mcp:no-mcp-session-id][{method}][Request] Message handling failed due to missing Mcp-Session-Id header");
                context.Response.StatusCode = 400;
                context.Response.SetContent("Missing Mcp-Session-Id header"u8.ToArray());
                await context.Response.AnswerAsync();
                return;
            }
            if (method != RequestMethods.NotificationsInitialized
                && !_manager.TryGetSession<TouchSocketServerTransportSession>(sessionId, out _))
            {
                Log.Warn($"[McpServer][TouchSocket][Mcp:{sessionId}][{method}][Request] Message handling failed due to unknown Mcp-Session-Id");
                context.Response.StatusCode = 400;
                context.Response.SetContent("Unknown Mcp-Session-Id"u8.ToArray());
                await context.Response.AnswerAsync();
                return;
            }
        }
        else
        {
            sessionId = _manager.MakeNewSessionId().ToString();
            context.Response.Headers.Add("Mcp-Session-Id", sessionId);
        }

        Log.Debug($"[McpServer][TouchSocket][Mcp:{sessionId}][{method}][Request] Handling JSON-RPC message[{message.Id}]");
        var response = await _manager.HandleRequestAsync(message,
            s => s.AddHttpTransportServices(sessionId, context.Request));

        if (response is null)
        {
            Log.Debug($"[McpServer][TouchSocket][Mcp:{sessionId}][{method}][Response] No response for message[{message.Id}] (notification)");
            context.Response.StatusCode = 202;
            await context.Response.AnswerAsync();
            return;
        }

        Log.Debug($"[McpServer][TouchSocket][Mcp:{sessionId}][{method}][Response] Sending response for message[{message.Id}]");
        await context.RespondJsonRpcAsync(_manager, 200, response);
    }

    private async ValueTask HandleStreamableHttpDisconnectionAsync(HttpContext context)
    {
        var sessionId = context.Request.Headers.Get("Mcp-Session-Id");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Log.Warn($"[McpServer][TouchSocket][Mcp:no-mcp-session-id] Disconnecting failed due to missing Mcp-Session-Id header");
            context.Response.StatusCode = 400;
            context.Response.SetContent("Missing Mcp-Session-Id header"u8.ToArray());
            await context.Response.AnswerAsync();
            return;
        }

        if (!_manager.TryGetSession<TouchSocketServerTransportSession>(sessionId, out var session))
        {
            Log.Debug($"[McpServer][TouchSocket][Mcp:{sessionId}] Disconnected but session not found (already terminated?)");
            context.Response.StatusCode = 200;
            await context.Response.AnswerAsync();
            return;
        }

        await session.DisposeAsync();
        Log.Info($"[McpServer][TouchSocket][Mcp:{sessionId}] Disconnected");
        context.Response.StatusCode = 200;
        await context.Response.AnswerAsync();
    }

    /// <summary>
    /// 按照 MCP 官方协议规范对传输层的要求：<br/>
    /// 服务器必须验证所有传入连接的 Origin 标头，以防止 DNS 重绑定攻击。<br/>
    /// Servers MUST validate the Origin header on all incoming connections to prevent DNS rebinding attacks<br/>
    /// </summary>
    /// <returns>如果验证失败，返回错误状态码和消息；否则返回 <see langword="null"/>。</returns>
    private (string statusCode, string message)? ValidateRequest(HttpContext context)
    {
        var request = context.Request;
        var isPost = request.Method == "POST";

        // 1. 验证 Content-Type（所有 POST 请求都需要）。
        if (isPost)
        {
            var contentType = request.Headers.Get("Content-Type");
            if (!ValidateContentType(contentType))
            {
                return ("400", "Invalid Content-Type header. Expected: application/json");
            }
        }

        // 2. DNS 重绑定防护（可选，默认禁用）。
        // Skip remaining validation if DNS rebinding protection is disabled.
        if (!_options.EnableDnsRebindingProtection)
        {
            return null;
        }

        // 3. 验证 Host header。
        // Validate Host header to prevent DNS rebinding attacks.
        var host = request.Headers.Get("Host");
        if (!ValidateHost(host))
        {
            return ("421", "Invalid Host header. Expected: localhost, 127.0.0.1, or [::1]");
        }

        // 4. 验证 Origin header（MCP 2025-11-25 新增要求，PR #1439）。
        // Validate Origin header - servers must respond with HTTP 403 Forbidden for invalid Origin headers.
        var origin = request.Headers.Get("Origin");
        if (!ValidateOrigin(origin))
        {
            return ("403", "Invalid Origin header. Expected: null or localhost origins");
        }

        return null;
    }

    /// <summary>
    /// 验证 Content-Type 是否为 application/json。
    /// </summary>
    private static bool ValidateContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        // 支持 "application/json" 和 "application/json; charset=utf-8" 等格式。
        return contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证 Host header 是否为本机地址（防止 DNS 重绑定攻击）。
    /// </summary>
    private static bool ValidateHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        // 移除端口号进行比较。
        var hostWithoutPort = host.Split(':')[0];
        return hostWithoutPort.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || hostWithoutPort.Equals("127.0.0.1", StringComparison.Ordinal)
               || hostWithoutPort.Equals("[::1]", StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Origin header 是否为本机来源（防止跨域攻击）。<br/>
    /// 按照 MCP 官方协议规范对传输层的要求：<br/>
    /// 如果 Origin 标头存在但无效，服务器必须返回 HTTP 403 Forbidden 错误。HTTP 响应正文可以包含一个没有 id 的 JSON-RPC 错误响应。<br/>
    /// If the Origin header is present and invalid, servers MUST respond with HTTP 403 Forbidden. The HTTP response body MAY comprise a JSON-RPC error response that has no id.<br/>
    /// </summary>
    private static bool ValidateOrigin(string? origin)
    {
        // Origin header 为空或 null 是允许的（非浏览器客户端）。
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        // "null" 是浏览器在某些情况下发送的特殊值（例如 file:// 协议）。
        if (origin.Equals("null", StringComparison.Ordinal))
        {
            return true;
        }

        // 验证是否为本机 Origin。
        return origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase)
               || origin.StartsWith("http://127.0.0.1", StringComparison.Ordinal)
               || origin.StartsWith("http://[::1]", StringComparison.Ordinal);
    }

    #endregion

    #region MCP SSE 协议（仅限兼容）

    /// <summary>
    /// 处理旧协议（2024-11-05）的 SSE 连接。<br/>
    /// 参考: <a href="https://modelcontextprotocol.io/specification/2024-11-05/basic/transports#http-with-sse">HTTP with SSE</a>
    /// </summary>
    private async Task HandleLegacySseConnectionAsync(HttpContext context)
    {
        var sessionId = SessionId.MakeNew().Id;

        context.Response.SetSseResponseHeaders();

        using var memoryStream = new MemoryStream();
        var writer = new StreamWriter(memoryStream, Encoding.UTF8) { AutoFlush = true };
        var session = new LegacySseSession(sessionId, writer, new CancellationTokenSource());

        if (!_legacySseSessions.TryAdd(sessionId, session))
        {
            throw new UnreachableException($"Session ID collision: '{sessionId}'");
        }

        Log.Info($"[McpServer][TouchSocket][Legacy:Sse:{sessionId}] Connection established");

        try
        {
            // 旧协议要求：发送 endpoint 事件告知客户端消息发送地址。
            await writer.WriteAsync($"id:{sessionId}\n");
            await writer.WriteAsync($"event:endpoint\n");
            await writer.WriteAsync($"data:{_options.SseMessageEndPoint}?sessionId={sessionId}\n\n");
            await writer.FlushAsync();

            context.Response.SetContent(memoryStream.ToArray());
            await context.Response.AnswerAsync();

            await Task.Delay(Timeout.Infinite, session.CancellationToken.Token);
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
        catch (Exception ex)
        {
            Log.Warn($"[McpServer][TouchSocket][Legacy:Sse:{sessionId}] Connection error", ex);
        }
        finally
        {
            _legacySseSessions.TryRemove(sessionId, out _);
            Log.Info($"[McpServer][TouchSocket][Legacy:Sse:{sessionId}] Connection closed");
            await writer.DisposeAsync();
        }
    }

    /// <summary>
    /// 处理旧协议（2024-11-05）的消息请求（通过 query string 传递 sessionId）。<br/>
    /// 参考: <a href="https://modelcontextprotocol.io/specification/2024-11-05/basic/transports#http-with-sse">HTTP with SSE</a>
    /// </summary>
    private async Task HandleLegacyMessageRequestAsync(HttpContext context)
    {
        var sessionId = context.Request.Query.Get("sessionId");

        if (string.IsNullOrEmpty(sessionId))
        {
            Log.Debug($"[McpServer][TouchSocket][Legacy:Message] Missing sessionId parameter");
            context.Response.StatusCode = 400;
            context.Response.SetContent("Missing sessionId parameter"u8.ToArray());
            await context.Response.AnswerAsync();
            return;
        }

        if (!_legacySseSessions.TryGetValue(sessionId, out var session))
        {
            Log.Debug($"[McpServer][TouchSocket][Legacy:Message:{sessionId}] Session not found");
            context.Response.StatusCode = 400;
            context.Response.SetContent(Encoding.UTF8.GetBytes($"Session not found: {sessionId}"));
            await context.Response.AnswerAsync();
            return;
        }

        if (session.Writer is null)
        {
            Log.Debug($"[McpServer][TouchSocket][Legacy:Message:{sessionId}] Session has no active SSE connection");
            context.Response.StatusCode = 400;
            context.Response.SetContent(Encoding.UTF8.GetBytes($"Session has no active SSE connection: {sessionId}"));
            await context.Response.AnswerAsync();
            return;
        }

        try
        {
            var bodyBytes = await context.Request.GetContentAsync();
            var message = await _manager.ReadRequestAsync(bodyBytes);
            if (message is null)
            {
                Log.Debug($"[McpServer][TouchSocket][Legacy:Message:{sessionId}] Invalid JSON-RPC message");
                context.Response.StatusCode = 400;
                context.Response.SetContent("Invalid JSON-RPC message"u8.ToArray());
                await context.Response.AnswerAsync();
                return;
            }

            Log.Debug($"[McpServer][TouchSocket][Legacy:Message:{sessionId}][{message.Method}][Request] Handling message[{message.Id}]");
            var response = await _manager.HandleRequestAsync(message,
                s => s.AddHttpTransportServices(sessionId, context.Request));

            // Notification：不返回内容。
            // Request：返回 SSE 消息。
            if (response is not null)
            {
                Log.Debug(
                    $"[McpServer][TouchSocket][Legacy:Message:{sessionId}][{message.Method}][Response] Sending SSE response for message[{message.Id}]");
                await session.Writer.WriteAsync("event:message\n");
                await session.Writer.WriteAsync("data:");
                await _manager.WriteResponseAsync(session.Writer.BaseStream, response, CancellationToken.None);
                await session.Writer.WriteAsync("\n\n");
                context.Response.StatusCode = 200;
                await context.Response.AnswerAsync();
            }
            else
            {
                // Notification：根据 MCP 协议，必须返回 202 Accepted。
                Log.Debug($"[McpServer][TouchSocket][Legacy:Message:{sessionId}][{message.Method}][Response] Notification received, returning 202 Accepted");
                context.Response.StatusCode = 202;
                await context.Response.AnswerAsync();
            }
        }
        catch (JsonException ex)
        {
            Log.Error($"[McpServer][TouchSocket][Legacy:Message:{sessionId}] JSON parsing error", ex);
            context.Response.StatusCode = 400;
            context.Response.SetContent("Invalid JSON"u8.ToArray());
            await context.Response.AnswerAsync();
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][TouchSocket][Legacy:Message:{sessionId}] Request handling error", ex);
            context.Response.StatusCode = 500;
            await context.Response.AnswerAsync();
        }
    }

    #endregion

    /// <summary>
    /// MCP HTTP 请求处理插件
    /// </summary>
    private sealed class McpHttpPlugin(TouchSocketServerTransport transport) : PluginBase, IHttpPlugin
    {
        public async Task OnHttpRequest(IHttpSessionClient client, HttpContextEventArgs e)
        {
            try
            {
                await transport.HandleRequestAsync(e);
            }
            catch (Exception ex)
            {
                transport.Log.Error($"[McpServer][TouchSocket][Http] Unhandled exception in HandleRequestAsync", ex);
                try
                {
                    await e.Context.Response
                        .SetStatus(500, "Internal Server Error")
                        .AnswerAsync();
                }
                catch
                {
                    // 可能连接已关闭
                }
            }
        }
    }

    /// <summary>
    /// 旧协议（2024-11-05）SSE 会话信息。
    /// </summary>
    private readonly record struct LegacySseSession(string SessionId, StreamWriter? Writer, CancellationTokenSource CancellationToken);
}

file static class Extensions
{
    extension(HttpContext context)
    {
        /// <summary>
        /// 返回 JSON-RPC 响应。
        /// </summary>
        /// <param name="manager">服务端传输管理器。</param>
        /// <param name="statusCode">HTTP 状态码。</param>
        /// <param name="response">JSON-RPC 响应对象。</param>
        internal async ValueTask RespondJsonRpcAsync(IServerTransportManager manager, int statusCode, JsonRpcResponse response)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            using var ms = new MemoryStream();
            await manager.WriteResponseAsync(ms, response, CancellationToken.None);
            context.Response.SetContent(ms.ToArray());
            await context.Response.AnswerAsync();
        }
    }

    extension(HttpResponse response)
    {
        /// <summary>
        /// 设置 CORS 相关的响应头。
        /// </summary>
        internal void SetCorsHeaders()
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Mcp-Session-Id");
        }

        /// <summary>
        /// 设置 SSE 相关的响应头。
        /// </summary>
        internal void SetSseResponseHeaders()
        {
            response.ContentType = "text/event-stream";
            response.Headers.Add("Cache-Control", "no-cache,no-store");
            response.Headers.Add("Content-Encoding", "identity");
            response.Headers.Add("Connection", "keep-alive");
        }
    }

    extension(IMcpServiceCollection services)
    {
        internal IMcpServiceCollection AddHttpTransportServices(string sessionId, HttpRequest request)
        {
            // 将 TouchSocket 的 HttpHeaders 转换为 NameValueCollection
            var nameValueCollection = new NameValueCollection();
            foreach (var header in request.Headers)
            {
                nameValueCollection.Add(header.Key, header.Value);
            }

            var context = new HttpServerTransportContext
            {
                SessionId = sessionId,
                Headers = nameValueCollection,
            };
            services.AddScoped(context);
            return services;
        }
    }
}
