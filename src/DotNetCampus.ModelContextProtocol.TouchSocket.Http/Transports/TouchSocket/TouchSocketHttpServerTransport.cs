using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Hosting.Services;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Servers;
using DotNetCampus.ModelContextProtocol.Transports.Http;
using TouchSocket.Core;
using TouchSocket.Http;
using TouchSocket.Sockets;

namespace DotNetCampus.ModelContextProtocol.Transports.TouchSocket;

/// <summary>
/// 基于 TouchSocket.Http 的 Streamable HTTP 传输层实现。
/// </summary>
/// <remarks>
/// TouchSocket.Http 的服务端传输层暂时没考虑兼容旧的 SSE 传输层协议（2024-11-05），
/// 若要兼容 SSE，请使用 MCP 库自带的 <see cref="LocalHostHttpServerTransport"/> 传输层。
/// </remarks>
public class TouchSocketHttpServerTransport : PluginBase, IHttpPlugin, IServerTransport
{
    private const string ProtocolVersionHeader = "MCP-Protocol-Version";
    private const string SessionIdHeader = "Mcp-Session-Id";
    private static readonly ReadOnlyMemory<byte> PrimeEventBytes = ": \n\n"u8.ToArray();

    private readonly IServerTransportManager _manager;
    private readonly ITouchSocketHttpServerTransportOptions _options;
    private readonly ConcurrentDictionary<string, TouchSocketHttpServerTransportSession> _sessions = new();

    private readonly TouchSocketConfig? _config;
    private readonly HttpService? _httpService;

    /// <summary>
    /// 初始化 <see cref="TouchSocketHttpServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="options">TouchSocket HTTP 传输层配置选项。</param>
    public TouchSocketHttpServerTransport(IServerTransportManager manager, ExternalTouchSocketHttpServerTransportOptions options)
    {
        _manager = manager;
        _options = options;
    }

    /// <summary>
    /// 初始化 <see cref="TouchSocketHttpServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="options">TouchSocket HTTP 传输层配置选项。</param>
    public TouchSocketHttpServerTransport(IServerTransportManager manager, TouchSocketHttpServerTransportOptions options)
    {
        _httpService = new HttpService();
        _manager = manager;
        _options = options;
        _config = new TouchSocketConfig()
            .SetListenIPHosts(options.Listen.Select(x => new IPHost(x)).ToArray())
            .SetServerName(_manager.ServerName)
            .ConfigurePlugins(p =>
            {
                p.Add(this);
                p.UseDefaultHttpServicePlugin();
            });
    }

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public async Task<Task> StartAsync(CancellationToken startingCancellationToken, CancellationToken runningCancellationToken)
    {
        if (_httpService is not null)
        {
            await _httpService.SetupAsync(_config!);
            await _httpService.StartAsync(startingCancellationToken);

            Log.Info($"[McpServer][TouchSocket] listening on {string.Join(", ", _httpService.Monitors
                .Select(x => x.Option.IpHost.ToString()))}, endpoint: {_options.EndPoint}");
        }
        else
        {
            Log.Info(
                $"[McpServer][TouchSocket] TouchSocketHttpServerTransport started in an external TouchSocket.Http.HttpServer, endpoint: {_options.EndPoint}");
        }

        return Task.Delay(Timeout.Infinite, runningCancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_httpService is { } httpService)
        {
            await httpService.StopAsync();
            Log.Info($"[McpServer][TouchSocket] TouchSocketHttpServerTransport stopped.");
            httpService.Dispose();
        }
        else
        {
            Log.Info($"[McpServer][TouchSocket] TouchSocketHttpServerTransport stopped. External TouchSocket.Http.HttpServer is still alive.");
        }
    }

    #region HTTP

    public async Task OnHttpRequest(IHttpSessionClient client, HttpContextEventArgs e)
    {
        try
        {
            await HandleRequestAsync(client, e);
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][TouchSocket][Http] Unhandled exception in HandleRequestAsync", ex);
            try
            {
                await e.Context.Response
                    .SetStatus(HttpStatusCode.InternalServerError, ex.Message)
                    .SetContent(ex.ToString())
                    .AnswerAsync();
            }
            catch
            {
                // 可能连接已关闭。
            }
        }
    }

    private async Task HandleRequestAsync(IHttpSessionClient client, HttpContextEventArgs e)
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
            await context.Response
                .SetStatus(statusCode, message)
                .SetContent("")
                .AnswerAsync();
            return;
        }

        context.Response.SetCorsHeaders();

        var method = context.Request.Method.ToString();

        // Streamable HTTP: 客户端建立连接。
        if (method == "GET" && endpoint.Equals(_options.EndPoint, StringComparison.OrdinalIgnoreCase))
        {
            await HandleStreamableHttpConnectionAsync(context, CancellationToken.None);
            return;
        }

        // Streamable HTTP: 客户端发送消息。
        if (method == "POST" && endpoint.Equals(_options.EndPoint, StringComparison.OrdinalIgnoreCase))
        {
            await HandleStreamableHttpMessageAsync(context, CancellationToken.None);
            return;
        }

        // Streamable HTTP: 客户端关闭连接。
        if (method == "DELETE" && endpoint.Equals(_options.EndPoint, StringComparison.OrdinalIgnoreCase))
        {
            await HandleStreamableHttpDisconnectionAsync(context);
            return;
        }

        Log.Warn($"[McpServer][TouchSocket] No handler found for {method} {endpoint}");
        await e.InvokeNext();
    }

    #endregion

    #region MCP Streamable HTTP 协议

    /// <summary>
    /// Streamable HTTP: 客户端建立 SSE 连接 (GET /mcp)。
    /// </summary>
    private async ValueTask HandleStreamableHttpConnectionAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;

        // 协商检查
        var accept = request.Headers.Get("Accept");
        if (!accept.Any(x => x.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase)))
        {
            // 规范 §2.2.3: return HTTP 405 Method Not Allowed indicating the server does not offer an SSE stream [if not accepted]
            Log.Warn($"[McpServer][TouchSocket][Mcp:no-session] GET request rejected: Client must accept text/event-stream");
            await context.RespondHttpError(HttpStatusCode.MethodNotAllowed, "Client must accept text/event-stream");
            return;
        }

        var sessionId = request.Headers.Get(SessionIdHeader).First;
        if (string.IsNullOrEmpty(sessionId))
        {
            Log.Warn($"[McpServer][TouchSocket][Mcp:no-session] GET request rejected: Missing Mcp-Session-Id header");
            await context.RespondHttpError(HttpStatusCode.NotFound, "Missing Mcp-Session-Id header");
            return;
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            Log.Warn($"[McpServer][TouchSocket][Mcp:{sessionId}] GET request rejected: Session not found");
            await context.RespondHttpError(HttpStatusCode.NotFound, "Session not found");
            return;
        }

        Log.Info($"[McpServer][TouchSocket][Mcp:{sessionId}] Establishing SSE connection");

        context.Response.SetStatus(HttpStatusCode.OK, "");
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Add("Cache-Control", "no-cache");

        try
        {
            context.Response.IsChunk = true;
            await using var output = context.Response.CreateWriteStream();
            await output.WriteAsync(PrimeEventBytes, cancellationToken);
            await output.FlushAsync(cancellationToken);

            await session.RunSseConnectionAsync(output, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Debug($"[McpServer][TouchSocket][Mcp:{sessionId}] SSE connection ended: {ex.Message}");
        }
        finally
        {
            await context.Response.CompleteChunkAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Streamable HTTP: 客户端发送消息 (POST /mcp)。
    /// </summary>
    private async ValueTask HandleStreamableHttpMessageAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;

        // 协议版本检查
        var protocolVersion = request.Headers.Get(ProtocolVersionHeader).First;
        if (!string.IsNullOrEmpty(protocolVersion))
        {
            // 如果比最小版本小则报错
            if (string.CompareOrdinal(protocolVersion, ProtocolVersion.Minimum) < 0)
            {
                Log.Warn($"[McpServer][TouchSocket] POST request rejected: Unsupported protocol version {protocolVersion}");
                await context.RespondHttpError(HttpStatusCode.BadRequest, $"Unsupported protocol version. Minimum required: {ProtocolVersion.Minimum}");
                return;
            }
        }

        JsonRpcRequest? jsonRpcRequest;
        try
        {
            var bodyBytes = await request.GetContentAsync();
            jsonRpcRequest = await _manager.ReadRequestAsync(bodyBytes);
        }
        catch (JsonException)
        {
            Log.Warn($"[McpServer][TouchSocket] POST request rejected: Invalid JSON");
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Invalid JSON");
            return;
        }

        if (jsonRpcRequest == null)
        {
            Log.Warn($"[McpServer][TouchSocket] POST request rejected: Empty body");
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Empty body");
            return;
        }

        var isInitialize = jsonRpcRequest.Method == RequestMethods.Initialize;
        var sessionIdStr = request.Headers.Get(SessionIdHeader).First;
        TouchSocketHttpServerTransportSession? session;

        if (isInitialize)
        {
            // 初始化请求，创建新 Session
            var newSessionId = _manager.MakeNewSessionId();
            var newSession = new TouchSocketHttpServerTransportSession(_manager, newSessionId.Id);

            if (_sessions.TryAdd(newSessionId.Id, newSession))
            {
                session = newSession;
                _manager.Add(session);
                context.Response.Headers.Add(SessionIdHeader, newSessionId.Id);
                Log.Info($"[McpServer][TouchSocket][Mcp:{newSessionId.Id}] New session created");
            }
            else
            {
                Log.Error($"[McpServer][TouchSocket] Session ID collision: {newSessionId.Id}");
                await context.RespondHttpError(HttpStatusCode.InternalServerError, "Session ID collision");
                return;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(sessionIdStr))
            {
                Log.Warn($"[McpServer][TouchSocket][Mcp:no-session][{jsonRpcRequest.Method}] POST request rejected: Missing Mcp-Session-Id header");
                await context.RespondHttpError(HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
                return;
            }

            if (!_sessions.TryGetValue(sessionIdStr, out session))
            {
                Log.Warn($"[McpServer][TouchSocket][Mcp:{sessionIdStr}][{jsonRpcRequest.Method}] POST request rejected: Session not found");
                await context.RespondHttpError(HttpStatusCode.NotFound, "Session not found");
                return;
            }
        }

        Log.Debug($"[McpServer][TouchSocket][Mcp:{session.SessionId}][{jsonRpcRequest.Method}][Request] Handling JSON-RPC message[{jsonRpcRequest.Id}]");

        var jsonRpcResponse = await _manager.HandleRequestAsync(jsonRpcRequest,
            s => s.AddHttpTransportServices(session.SessionId, request),
            cancellationToken: cancellationToken);

        if (jsonRpcResponse != null)
        {
            // Request: Success or Failed.
            Log.Debug($"[McpServer][TouchSocket][Mcp:{session.SessionId}][{jsonRpcRequest.Method}][Response] Sending response for message[{jsonRpcRequest.Id}]");
            await context.RespondJsonRpcAsync(_manager, HttpStatusCode.OK, jsonRpcResponse);
        }
        else
        {
            // Notification: No need to respond.
            Log.Debug($"[McpServer][TouchSocket][Mcp:{session.SessionId}][{jsonRpcRequest.Method}][Response] No response for message[{jsonRpcRequest.Id}] (notification)");
            await context.RespondHttpSuccess(HttpStatusCode.Accepted);
        }
    }

    /// <summary>
    /// Streamable HTTP: 客户端关闭连接 (DELETE /mcp)。
    /// </summary>
    private async ValueTask HandleStreamableHttpDisconnectionAsync(HttpContext context)
    {
        var sessionId = context.Request.Headers.Get(SessionIdHeader).First;
        if (!string.IsNullOrEmpty(sessionId))
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                await session.DisposeAsync();
                Log.Info($"[McpServer][TouchSocket][Mcp:{sessionId}] Session terminated");
            }
            else
            {
                Log.Debug($"[McpServer][TouchSocket][Mcp:{sessionId}] DELETE request: Session not found (already terminated?)");
            }
        }
        await context.RespondHttpSuccess(HttpStatusCode.OK);
    }

    /// <summary>
    /// 按照 MCP 官方协议规范对传输层的要求：<br/>
    /// 服务器必须验证所有传入连接的 Origin 标头，以防止 DNS 重绑定攻击。<br/>
    /// Servers MUST validate the Origin header on all incoming connections to prevent DNS rebinding attacks<br/>
    /// </summary>
    /// <returns>如果验证失败，返回错误状态码和消息；否则返回 <see langword="null"/>。</returns>
    private (int statusCode, string message)? ValidateRequest(HttpContext context)
    {
        var request = context.Request;
        var isPost = request.IsPost();

        // 1. 验证 Content-Type（所有 POST 请求都需要）。
        if (isPost)
        {
            var contentType = request.ContentType.First;
            if (!ValidateContentType(contentType))
            {
                return (HttpStatusCode.BadRequest, "Invalid Content-Type header. Expected: application/json");
            }
        }

        // 2. 验证 Accept header（所有 POST 请求都需要）。
        // 根据 MCP 2025-11-25 规范：客户端必须在 Accept header 中同时列出 application/json 和 text/event-stream
        // The client MUST include an Accept header, listing both application/json and text/event-stream as supported content types.
        if (isPost)
        {
            var accept = request.Headers.Get("Accept");
            if (!ValidateAccept(accept))
            {
                return (HttpStatusCode.NotAcceptable, "Not Acceptable: Client must accept both application/json and text/event-stream");
            }
        }

        // 3. DNS 重绑定防护（可选，默认禁用）。
        // Skip remaining validation if DNS rebinding protection is disabled.

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
    /// 验证 Accept header 是否包含必需的 MIME 类型。<br/>
    /// 根据 MCP 2025-11-25 规范：客户端必须在 Accept header 中同时列出 application/json 和 text/event-stream。<br/>
    /// 但为了兼容性，我们只验证至少包含其中一个（application/json 或 text/event-stream），
    /// 服务端会根据自己的能力返回合适的 Content-Type。<br/>
    /// The client MUST include an Accept header, listing both application/json and text/event-stream as supported content types.
    /// </summary>
    private static bool ValidateAccept(TextValues accept)
    {
        // Accept header 可能包含多个值，用逗号分隔，且可能带有 q 参数
        // 例如: "application/json, text/event-stream" 或 "application/json;q=0.9, text/event-stream"
        // 为了兼容性，只要包含 application/json 或 text/event-stream 中的任意一个即可
        return accept.Any(x => x.Contains("application/json", StringComparison.InvariantCultureIgnoreCase))
               || accept.Any(x => x.Contains("text/event-stream", StringComparison.InvariantCultureIgnoreCase));
    }

    #endregion
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
            context.Response.SetStatus(statusCode, "");

            context.Response.IsChunk = true;
            await using (var stream = context.Response.CreateWriteStream())
            {
                await manager.WriteMessageAsync(stream, response, CancellationToken.None);
            }
            await context.Response.CompleteChunkAsync();
        }

        /// <summary>
        /// 返回 HTTP 成功响应。
        /// </summary>
        /// <param name="statusCode">HTTP 状态码。</param>
        internal async ValueTask RespondHttpSuccess(int statusCode)
        {
            await context.Response
                .SetStatus(statusCode, "")
                .SetContent("")
                .AnswerAsync();
        }

        /// <summary>
        /// 返回 HTTP 错误（传输层错误）。
        /// </summary>
        internal async ValueTask RespondHttpError(int statusCode, string? message = null)
        {
            await context.Response
                .SetStatus(statusCode, message)
                .SetContent("")
                .AnswerAsync();
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
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Mcp-Session-Id, Mcp-Protocol-Version");
            // 根据 MCP 协议，必须暴露这些头部供客户端访问
            response.Headers.Add("Access-Control-Expose-Headers", "Mcp-Session-Id, Last-Event-Id, Mcp-Protocol-Version");
        }
    }

    extension(IMcpServiceCollection services)
    {
        internal IMcpServiceCollection AddHttpTransportServices(string sessionId, HttpRequest request)
        {
            var nameValueCollection = new NameValueCollection();
            foreach (var header in request.Headers)
            {
                nameValueCollection.Add(header.Key, header.Value.First);
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
