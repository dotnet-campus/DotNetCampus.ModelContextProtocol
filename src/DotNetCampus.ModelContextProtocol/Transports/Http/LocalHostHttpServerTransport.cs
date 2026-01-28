using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Hosting.Services;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Servers;
using DotNetCampus.ModelContextProtocol.Utils;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// 仅限监听本机回环地址（localhost）的 Streamable HTTP 传输层实现。
/// </summary>
public class LocalHostHttpServerTransport : IServerTransport
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, false);
    private readonly IServerTransportManager _manager;
    private readonly LocalHostHttpServerTransportOptions _options;
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<string, LegacySseSession> _legacySseSessions = [];

    /// <summary>
    /// 初始化 <see cref="LocalHostHttpServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="options">Streamable HTTP 传输层配置选项。</param>
    public LocalHostHttpServerTransport(IServerTransportManager manager, LocalHostHttpServerTransportOptions options)
    {
        _manager = manager;
        _options = options;

        foreach (var prefix in options.GetUrlPrefixes())
        {
            _listener.Prefixes.Add(prefix);
        }
    }

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public Task<Task> StartAsync(CancellationToken startingCancellationToken, CancellationToken runningCancellationToken)
    {
        _listener.Start();
        Log.Info($"[McpServer][StreamableHttp] listening on {string.Join(", ", _listener.Prefixes)}, endpoint: {_options.EndPoint}");

        return Task.FromResult(RunLoopAsync(runningCancellationToken));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _listener.Stop();
        Log.Info($"[McpServer][StreamableHttp] stopped listening");
        _listener.Close();
        return ValueTask.CompletedTask;
    }

    #region HTTP

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                Log.Debug($"[McpServer][StreamableHttp][Http] Received request: {context.Request.HttpMethod} {context.Request.Url}");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleRequestAsync(context);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[McpServer][StreamableHttp][Http] Unhandled exception in HandleRequestAsync", ex);
                        try
                        {
                            context.RespondHttpError(HttpStatusCode.InternalServerError);
                        }
                        catch
                        {
                            // 可能连接已关闭。
                        }
                    }
                }, cancellationToken);
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995) // ERROR_OPERATION_ABORTED
            {
                // 正常关闭
                Log.Info($"[McpServer][StreamableHttp][Http] Listener stopped");
                break;
            }
            catch (Exception ex)
            {
                Log.Error($"[McpServer][StreamableHttp][Http] Error in GetContextAsync", ex);
            }
        }
    }

    private async ValueTask HandleRequestAsync(HttpListenerContext context)
    {
        var endpoint = context.Request.Url?.AbsolutePath;
        if (endpoint is null)
        {
            Log.Warn($"[McpServer][StreamableHttp][Http] Request with null URL from {context.Request.RemoteEndPoint}");
            context.RespondHttpError(HttpStatusCode.NotFound);
            return;
        }

        // 请求安全性验证。
        var validationError = ValidateRequest(context);
        if (validationError.HasValue)
        {
            Log.Warn($"[McpServer][StreamableHttp][Http] Request validation failed: {validationError.Value.statusCode} - {validationError.Value.message}");
            context.RespondHttpError(validationError.Value.statusCode, validationError.Value.message);
            return;
        }

        context.Response.SetCorsHeaders();

        if (context.Request.HttpMethod == "OPTIONS")
        {
            context.RespondHttpSuccess(HttpStatusCode.OK);
            return;
        }

        var method = context.Request.HttpMethod;

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

        Log.Warn($"[McpServer][StreamableHttp] No handler found for {method} {endpoint}");
        context.RespondHttpError(HttpStatusCode.NotFound);
    }

    #endregion

    #region MCP Streamable HTTP 协议

    /// <summary>
    /// Streamable HTTP: 客户端建立连接。
    /// </summary>
    private ValueTask HandleStreamableHttpConnectionAsync(HttpListenerContext context)
    {
        var sessionId = context.Request.Headers["Mcp-Session-Id"];
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Log.Warn($"[McpServer][StreamableHttp][Mcp:no-mcp-session-id] Connection failed due to missing Mcp-Session-Id header");
            context.RespondHttpError(HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
            return ValueTask.CompletedTask;
        }

        Log.Info($"[McpServer][StreamableHttp][Mcp:{sessionId}] Establishing connection");
        var session = new LocalHostHttpServerTransportSession(sessionId, context);
        _manager.Add(session);
        return session.WaitForDisconnectedAsync();
    }

    private async ValueTask HandleStreamableHttpMessageAsync(HttpListenerContext context)
    {
        var message = await _manager.ReadRequestAsync(context.Request.InputStream);
        if (message?.Method is not { } method)
        {
            Log.Warn($"[McpServer][StreamableHttp][Mcp:no-mcp-session-id] Invalid JSON-RPC message received");
            context.RespondHttpError(HttpStatusCode.BadRequest, "Invalid JSON-RPC message");
            return;
        }

        var sessionId = context.Request.Headers["Mcp-Session-Id"];
        if (method != RequestMethods.Initialize)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                Log.Warn($"[McpServer][StreamableHttp][Mcp:no-mcp-session-id][{method}][Request] Message handling failed due to missing Mcp-Session-Id header");
                context.RespondHttpError(HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
                return;
            }
            if (method != RequestMethods.NotificationsInitialized
                && !_manager.TryGetSession<LocalHostHttpServerTransportSession>(sessionId, out _))
            {
                Log.Warn($"[McpServer][StreamableHttp][Mcp:{sessionId}][{method}][Request] Message handling failed due to unknown Mcp-Session-Id");
                context.RespondHttpError(HttpStatusCode.BadRequest, "Unknown Mcp-Session-Id");
                return;
            }
        }
        else
        {
            sessionId = _manager.MakeNewSessionId().ToString();
            context.Response.Headers.Add("Mcp-Session-Id", sessionId);
        }

        Log.Debug($"[McpServer][StreamableHttp][Mcp:{sessionId}][{method}][Request] Handling JSON-RPC message[{message.Id}]");
        var response = await _manager.HandleRequestAsync(message,
            s => s.AddHttpTransportServices(sessionId, context.Request.Headers));

        if (response is null)
        {
            Log.Debug($"[McpServer][StreamableHttp][Mcp:{sessionId}][{method}][Response] No response for message[{message.Id}] (notification)");
            context.RespondHttpSuccess(HttpStatusCode.Accepted);
            return;
        }

        Log.Debug($"[McpServer][StreamableHttp][Mcp:{sessionId}][{method}][Response] Sending response for message[{message.Id}]");
        await context.RespondJsonRpcAsync(HttpStatusCode.OK, response);
    }

    private async ValueTask HandleStreamableHttpDisconnectionAsync(HttpListenerContext context)
    {
        var sessionId = context.Request.Headers["Mcp-Session-Id"];
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Log.Warn($"[McpServer][StreamableHttp][Mcp:no-mcp-session-id] Disconnecting failed due to missing Mcp-Session-Id header");
            context.RespondHttpError(HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
            return;
        }

        if (!_manager.TryGetSession<LocalHostHttpServerTransportSession>(sessionId, out var session))
        {
            Log.Debug($"[McpServer][StreamableHttp][Mcp:{sessionId}] Disconnected but session not found (already terminated?)");
            context.RespondHttpSuccess(HttpStatusCode.OK);
            return;
        }

        await session.DisposeAsync();
        Log.Info($"[McpServer][StreamableHttp][Mcp:{sessionId}] Disconnected");
        context.RespondHttpSuccess(HttpStatusCode.OK);
    }

    /// <summary>
    /// 按照 MCP 官方协议规范对传输层的要求：<br/>
    /// 服务器必须验证所有传入连接的 Origin 标头，以防止 DNS 重绑定攻击。<br/>
    /// Servers MUST validate the Origin header on all incoming connections to prevent DNS rebinding attacks<br/>
    /// </summary>
    /// <returns>如果验证失败，返回错误状态码和消息；否则返回 <see langword="null"/>。</returns>
    private (HttpStatusCode statusCode, string message)? ValidateRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var isPost = request.HttpMethod == "POST";

        // 1. 验证 Content-Type（所有 POST 请求都需要）。
        if (isPost)
        {
            var contentType = request.ContentType;
            if (!ValidateContentType(contentType))
            {
                return (HttpStatusCode.BadRequest, "Invalid Content-Type header. Expected: application/json");
            }
        }

        // 2. DNS 重绑定防护（可选，默认启用）。
        // Skip remaining validation if DNS rebinding protection is disabled.
        if (!_options.EnableDnsRebindingProtection)
        {
            return null;
        }

        // 3. 验证 Host header。
        // Validate Host header to prevent DNS rebinding attacks.
        var host = request.Headers["Host"];
        if (!ValidateHost(host))
        {
            return (HttpStatusCode.MisdirectedRequest, "Invalid Host header. Expected: localhost, 127.0.0.1, or [::1]");
        }

        // 4. 验证 Origin header（MCP 2025-11-25 新增要求，PR #1439）。
        // Validate Origin header - servers must respond with HTTP 403 Forbidden for invalid Origin headers.
        var origin = request.Headers["Origin"];
        if (!ValidateOrigin(origin))
        {
            return (HttpStatusCode.Forbidden, "Invalid Origin header. Expected: null or localhost origins");
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
    private bool ValidateHost(string? host)
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
    private async Task HandleLegacySseConnectionAsync(HttpListenerContext context)
    {
        var sessionId = SessionId.MakeNew().Id;

        context.Response.SetSseResponseHeaders();

        var writer = new StreamWriter(context.Response.OutputStream, Utf8) { AutoFlush = true };
        var session = new LegacySseSession(sessionId, writer, new CancellationTokenSource());

        if (!_legacySseSessions.TryAdd(sessionId, session))
        {
            throw new UnreachableException($"Session ID collision: '{sessionId}'");
        }

        Log.Info($"[McpServer][StreamableHttp][Legacy:Sse:{sessionId}] Connection established");

        try
        {
            // 旧协议要求：发送 endpoint 事件告知客户端消息发送地址。
            await writer.WriteAsync($"id:{sessionId}\n");
            await writer.WriteAsync($"event:endpoint\n");
            await writer.WriteAsync($"data:{_options.SseMessageEndPoint}?sessionId={sessionId}\n\n");

            await Task.Delay(Timeout.Infinite, session.CancellationToken.Token);
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
        catch (Exception ex)
        {
            Log.Warn($"[McpServer][StreamableHttp][Legacy:Sse:{sessionId}] Connection error", ex);
        }
        finally
        {
            _legacySseSessions.TryRemove(sessionId, out _);
            Log.Info($"[McpServer][StreamableHttp][Legacy:Sse:{sessionId}] Connection closed");
            await writer.DisposeAsync();
        }
    }

    /// <summary>
    /// 处理旧协议（2024-11-05）的消息请求（通过 query string 传递 sessionId）。<br/>
    /// 参考: <a href="https://modelcontextprotocol.io/specification/2024-11-05/basic/transports#http-with-sse">HTTP with SSE</a>
    /// </summary>
    private async Task HandleLegacyMessageRequestAsync(HttpListenerContext context)
    {
        var sessionId = context.Request.QueryString["sessionId"];

        if (string.IsNullOrEmpty(sessionId))
        {
            Log.Debug($"[McpServer][StreamableHttp][Legacy:Message] Missing sessionId parameter");
            context.RespondHttpError(HttpStatusCode.BadRequest, "Missing sessionId parameter");
            return;
        }

        if (!_legacySseSessions.TryGetValue(sessionId, out var session))
        {
            Log.Debug($"[McpServer][StreamableHttp][Legacy:Message:{sessionId}] Session not found");
            context.RespondHttpError(HttpStatusCode.BadRequest, $"Session not found: {sessionId}");
            return;
        }

        if (session.Writer is null)
        {
            Log.Debug($"[McpServer][StreamableHttp][Legacy:Message:{sessionId}] Session has no active SSE connection");
            context.RespondHttpError(HttpStatusCode.BadRequest, $"Session has no active SSE connection: {sessionId}");
            return;
        }

        try
        {
            var message = await _manager.ReadRequestAsync(context.Request.InputStream);
            if (message is null)
            {
                Log.Debug($"[McpServer][StreamableHttp][Legacy:Message:{sessionId}] Invalid JSON-RPC message");
                context.RespondHttpError(HttpStatusCode.BadRequest, "Invalid JSON-RPC message");
                return;
            }

            Log.Debug($"[McpServer][StreamableHttp][Legacy:Message:{sessionId}][{message.Method}][Request] Handling message[{message.Id}]");
            var response = await _manager.HandleRequestAsync(message,
                s => s.AddHttpTransportServices(sessionId, context.Request.Headers));

            // Notification：不返回内容。
            // Request：返回 SSE 消息。
            if (response is not null)
            {
                Log.Debug(
                    $"[McpServer][StreamableHttp][Legacy:Message:{sessionId}][{message.Method}][Response] Sending SSE response for message[{message.Id}]");
                await session.Writer.WriteAsync("event:message\n");
                var responseText = JsonSerializer.Serialize(response, McpServerResponseJsonContext.Default.JsonRpcResponse);
                await session.Writer.WriteAsync($"data:{responseText}\n\n");
                context.RespondHttpSuccess(HttpStatusCode.OK);
            }
            else
            {
                // Notification：根据 MCP 协议，必须返回 202 Accepted。
                Log.Debug($"[McpServer][StreamableHttp][Legacy:Message:{sessionId}][{message.Method}][Response] Notification received, returning 202 Accepted");
                context.RespondHttpSuccess(HttpStatusCode.Accepted);
            }
        }
        catch (JsonException ex)
        {
            Log.Error($"[McpServer][StreamableHttp][Legacy:Message:{sessionId}] JSON parsing error", ex);
            context.RespondHttpError(HttpStatusCode.BadRequest, "Invalid JSON");
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][StreamableHttp][Legacy:Message:{sessionId}] Request handling error", ex);
            context.RespondHttpError(HttpStatusCode.InternalServerError);
        }
    }

    #endregion

    /// <summary>
    /// 旧协议（2024-11-05）SSE 会话信息。
    /// </summary>
    private readonly record struct LegacySseSession(string SessionId, StreamWriter? Writer, CancellationTokenSource CancellationToken);
}

file static class Extensions
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, false);

    extension(HttpListenerContext context)
    {
        /// <summary>
        /// 返回 JSON-RPC 响应。
        /// </summary>
        /// <param name="statusCode">HTTP 状态码。</param>
        /// <param name="response">JSON-RPC 响应对象。</param>
        internal async ValueTask RespondJsonRpcAsync(HttpStatusCode statusCode, JsonRpcResponse response)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            await JsonSerializer.SerializeAsync(context.Response.OutputStream, response, McpServerResponseJsonContext.Default.JsonRpcResponse);
            context.Response.Close();
        }

        /// <summary>
        /// 返回 HTTP 成功响应。
        /// </summary>
        /// <param name="statusCode">HTTP 状态码。</param>
        internal void RespondHttpSuccess(HttpStatusCode statusCode)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.Close();
        }

        /// <summary>
        /// 返回 HTTP 错误（传输层错误）。
        /// </summary>
        internal void RespondHttpError(HttpStatusCode statusCode, string? message = null)
        {
            context.Response.StatusCode = (int)statusCode;

            if (!string.IsNullOrEmpty(message))
            {
                var errorBytes = Utf8.GetBytes(message);
                context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
            }

            context.Response.Close();
        }
    }

    extension(HttpListenerResponse response)
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
        internal IMcpServiceCollection AddHttpTransportServices(string sessionId, NameValueCollection headers)
        {
            var context = new HttpServerTransportContext
            {
                SessionId = sessionId,
                Headers = headers,
            };
            services.AddScoped(context);
            return services;
        }
    }
}
