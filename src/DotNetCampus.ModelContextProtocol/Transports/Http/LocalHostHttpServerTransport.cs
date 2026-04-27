using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Hosting.Services;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Transports.Http.Legacy;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// 仅限监听本机回环地址（localhost）的 Streamable HTTP 传输层实现。
/// </summary>
public class LocalHostHttpServerTransport : IServerTransport
{
    private const string ProtocolVersionHeader = "MCP-Protocol-Version";
    private const string SessionIdHeader = "Mcp-Session-Id";
    private const int SseKeepAliveIntervalMs = 60000;
    private static readonly ReadOnlyMemory<byte> PrimeEventBytes = ": \n\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> SseKeepAliveBytes = ": keep-alive\n\n"u8.ToArray();

    private readonly IServerTransportManager _manager;
    private readonly LocalHostHttpServerTransportOptions _options;
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<string, HttpServerTransportSession> _sessions = new();
    private readonly ConcurrentDictionary<string, LegacySseServerTransportSession> _legacySessions = new();

    /// <summary>
    /// 初始化 <see cref="LocalHostHttpServerTransport"/> 类的新实例。
    /// </summary>
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
        try
        {
            _listener.Start();
            Log.Info($"[McpServer][StreamableHttp] Listening on {string.Join(", ", _listener.Prefixes)}, endpoint: {_options.EndPoint}");

            return Task.FromResult(RunLoopAsync(runningCancellationToken));
        }
        catch (Exception ex)
        {
            Log.Critical($"[McpServer][StreamableHttp] Failed to start listener.", ex);
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        try
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }
            _listener.Close();
        }
        catch (Exception ex)
        {
            Log.Debug($"[McpServer][StreamableHttp] Exception during dispose. Error={ex.Message}");
        }
        return ValueTask.CompletedTask;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleRequestAsync(context, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[McpServer][StreamableHttp] Unhandled exception in request handler.", ex);
                        try
                        {
                            await context.RespondHttpError(HttpStatusCode.InternalServerError);
                        }
                        catch
                        {
                            // 忽略关闭时的异常
                        }
                    }
                }, cancellationToken);
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995) // ERROR_OPERATION_ABORTED
            {
                Log.Info($"[McpServer][StreamableHttp] Transport stopped.");
                break;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                Log.Error($"[McpServer][StreamableHttp] Accept loop error.", ex);
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        // 1. 路径检查
        var requestPath = request.Url?.AbsolutePath ?? "/";
        var isModernEndpoint = requestPath.Equals(_options.EndPoint, StringComparison.OrdinalIgnoreCase);
        var isLegacySseEndpoint = _options.IsCompatibleWithSse
                                  && requestPath.Equals(_options.SseEndPoint, StringComparison.OrdinalIgnoreCase);
        var isLegacyMessageEndpoint = _options.IsCompatibleWithSse
                                      && requestPath.Equals(_options.SseMessageEndPoint, StringComparison.OrdinalIgnoreCase);
        if (!isModernEndpoint && !isLegacySseEndpoint && !isLegacyMessageEndpoint)
        {
            await context.RespondHttpError(HttpStatusCode.NotFound);
            return;
        }

        // 2. 跨域/安全检查
        // 按照 MCP 2025-11-25 规范：Servers MUST validate the Origin header on all incoming connections to prevent DNS rebinding attacks.
        var origin = request.Headers["Origin"];
        if (!ValidateOrigin(origin))
        {
            await context.RespondHttpError(HttpStatusCode.Forbidden, "Invalid Origin header");
            return;
        }

        response.AppendHeader("Access-Control-Allow-Origin", origin ?? "*");
        response.AppendHeader("Access-Control-Allow-Methods", "POST, GET, DELETE, OPTIONS");
        response.AppendHeader("Access-Control-Allow-Headers", $"{SessionIdHeader}, {ProtocolVersionHeader}, Content-Type");
        response.AppendHeader("Access-Control-Expose-Headers", $"{SessionIdHeader}, {ProtocolVersionHeader}");

        if (request.HttpMethod == "OPTIONS")
        {
            context.RespondHttpSuccess(HttpStatusCode.OK);
            return;
        }

        switch (request.HttpMethod)
        {
            case "POST":
                if (isLegacyMessageEndpoint)
                {
                    await HandleLegacyPostRequestAsync(context, cancellationToken);
                }
                else if (isModernEndpoint)
                {
                    await HandlePostRequestAsync(context, cancellationToken);
                }
                else
                {
                    response.AddHeader("Allow", "GET, OPTIONS");
                    await context.RespondHttpError(HttpStatusCode.MethodNotAllowed);
                }
                break;
            case "GET":
                if (isLegacySseEndpoint)
                {
                    await HandleLegacyGetRequestAsync(context, cancellationToken);
                }
                else if (isModernEndpoint)
                {
                    await HandleGetRequestAsync(context, cancellationToken);
                }
                else
                {
                    response.AddHeader("Allow", "POST, DELETE, OPTIONS");
                    await context.RespondHttpError(HttpStatusCode.MethodNotAllowed);
                }
                break;
            case "DELETE":
                if (isModernEndpoint)
                {
                    await HandleDeleteRequestAsync(context);
                }
                else
                {
                    response.AddHeader("Allow", "POST, OPTIONS");
                    await context.RespondHttpError(HttpStatusCode.MethodNotAllowed);
                }
                break;
            default:
                response.AddHeader("Allow", "POST, GET, DELETE, OPTIONS");
                await context.RespondHttpError(HttpStatusCode.MethodNotAllowed);
                break;
        }
    }

    private async Task HandleLegacyGetRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var newSessionId = _manager.MakeNewSessionId();
        var session = new LegacySseServerTransportSession(_manager, newSessionId.Id, "[McpServer][LegacySse]");
        if (!_legacySessions.TryAdd(session.SessionId, session))
        {
            await context.RespondHttpError(HttpStatusCode.InternalServerError, "Session ID collision");
            return;
        }

        _manager.Add(session);

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";

        var output = context.Response.OutputStream;
        session.AttachSseStream(output);

        try
        {
            await output.WriteAsync(PrimeEventBytes, cancellationToken);
            await session.WriteEndpointEventAsync(output, BuildLegacyMessageEndpointUri(context.Request, session.SessionId), cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(SseKeepAliveIntervalMs, cancellationToken);
                await output.WriteAsync(SseKeepAliveBytes, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Info($"[McpServer][LegacySse] SSE connection ended. SessionId={session.SessionId}");
        }
        catch (Exception ex)
        {
            Log.Info($"[McpServer][LegacySse] SSE connection ended. SessionId={session.SessionId}, Error={ex.Message}");
        }
        finally
        {
            session.DetachSseStream(output);
            _legacySessions.TryRemove(session.SessionId, out _);
            await session.DisposeAsync();
            context.Response.SafeClose();
        }
    }

    private async Task HandleLegacyPostRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        JsonRpcMessage? message;
        try
        {
            message = await _manager.ReadMessageAsync(context.Request.InputStream);
        }
        catch (JsonException)
        {
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Invalid JSON");
            return;
        }
        catch
        {
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Failed to read request body");
            return;
        }

        var sessionId = context.Request.QueryString["sessionId"];
        if (message is not null)
        {
            _manager.LogRawIn("[LegacySse]", $"POST, SessionId={sessionId}", message);
        }

        switch (message)
        {
            case JsonRpcResponse jsonRpcResponse:
                await HandleLegacyClientResponseAsync(context, sessionId, jsonRpcResponse);
                return;
            case JsonRpcNotification notification:
                await HandleLegacyNotificationAsync(context, sessionId, notification, cancellationToken);
                return;
            case JsonRpcRequest jsonRpcRequest:
                await HandleLegacyRpcRequestAsync(context, sessionId, jsonRpcRequest, cancellationToken);
                return;
            default:
                await context.RespondHttpError(HttpStatusCode.BadRequest, "Invalid or unrecognized JSON-RPC message");
                return;
        }
    }

    private async Task HandleLegacyClientResponseAsync(HttpListenerContext context, string? sessionId, JsonRpcResponse response)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Missing sessionId query parameter");
            return;
        }

        if (!_legacySessions.TryGetValue(sessionId, out var session))
        {
            await context.RespondHttpError(HttpStatusCode.NotFound, "Session not found");
            return;
        }

        session.HandleResponseAsync(response);
        context.RespondHttpSuccess(HttpStatusCode.Accepted);
    }

    private async Task HandleLegacyNotificationAsync(HttpListenerContext context, string? sessionId, JsonRpcNotification notification,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Missing sessionId query parameter");
            return;
        }

        if (!_legacySessions.TryGetValue(sessionId, out var session))
        {
            await context.RespondHttpError(HttpStatusCode.NotFound, "Session not found");
            return;
        }

        await _manager.HandleRequestAsync(
            new JsonRpcRequest { Method = notification.Method, Params = notification.Params },
            s => s.AddTransportSession(session, Log),
            cancellationToken);
        context.RespondHttpSuccess(HttpStatusCode.Accepted);
    }

    private async Task HandleLegacyRpcRequestAsync(HttpListenerContext context, string? sessionId, JsonRpcRequest jsonRpcRequest,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Missing sessionId query parameter");
            return;
        }

        if (!_legacySessions.TryGetValue(sessionId, out var session))
        {
            await context.RespondHttpError(HttpStatusCode.NotFound, "Session not found");
            return;
        }

        if (!session.HasActiveSseStream)
        {
            await context.RespondHttpError(HttpStatusCode.NotFound, "Session not connected");
            return;
        }

        var response = await _manager.HandleRequestAsync(
            jsonRpcRequest,
            s => s.AddTransportSession(session, Log),
            cancellationToken);

        if (response is not null)
        {
            if (jsonRpcRequest.Method == RequestMethods.Initialize)
            {
                response = LegacySseInitializeResponseAdapter.Adapt(response);
            }

            await session.WriteMessageEventAsync(response, cancellationToken);
        }

        context.RespondHttpSuccess(HttpStatusCode.Accepted);
    }

    private async Task HandlePostRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;

        // 协议版本检查
        // 按照 MCP 协议规范 §2.7：Streamable HTTP 传输层最低支持版本为 2025-03-26（该版本引入了 Streamable HTTP 传输层）。
        // If the server receives a request with an invalid or unsupported MCP-Protocol-Version, it MUST respond with 400 Bad Request.
        var protocolVersion = request.Headers[ProtocolVersionHeader];
        if (!string.IsNullOrEmpty(protocolVersion) && (ProtocolVersion)protocolVersion < ProtocolVersion.StreamableHttpMinimum)
        {
            await context.RespondHttpError(HttpStatusCode.BadRequest, $"Unsupported protocol version. Minimum required: {ProtocolVersion.StreamableHttpMinimum}");
            return;
        }

        // 解析消息体
        JsonRpcMessage? message;
        try
        {
            message = await _manager.ReadMessageAsync(request.InputStream);
        }
        catch (JsonException)
        {
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Invalid JSON");
            return;
        }
        catch
        {
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Failed to read request body");
            return;
        }

        var sessionIdStr = request.Headers[SessionIdHeader];

        if (message is not null)
        {
            _manager.LogRawIn("[StreamableHttp]", $"POST, SessionId={sessionIdStr}", message);
        }
        switch (message)
        {
            case JsonRpcResponse jsonRpcResponse:
                await HandleClientResponseAsync(context, sessionIdStr, jsonRpcResponse);
                return;
            case JsonRpcNotification notification:
                await HandleNotificationAsync(context, sessionIdStr, notification, cancellationToken);
                return;
            case JsonRpcRequest jsonRpcRequest:
                await HandleRpcRequestAsync(context, sessionIdStr, jsonRpcRequest, cancellationToken);
                return;
            default:
                await context.RespondHttpError(HttpStatusCode.BadRequest, "Invalid or unrecognized JSON-RPC message");
                return;
        }
    }

    /// <summary>
    /// 客户端响应服务器发起的请求（如 sampling/createMessage）。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sessionIdStr"></param>
    /// <param name="response"></param>
    private async Task HandleClientResponseAsync(HttpListenerContext context, string? sessionIdStr, JsonRpcResponse response)
    {
        if (string.IsNullOrEmpty(sessionIdStr) || !_sessions.TryGetValue(sessionIdStr, out var session))
        {
            await context.RespondHttpError(HttpStatusCode.NotFound, "Session not found");
            return;
        }
        session.HandleResponseAsync(response);
        context.RespondHttpSuccess(HttpStatusCode.Accepted);
    }

    /// <summary>
    /// 通知消息，无需响应。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sessionIdStr"></param>
    /// <param name="notification"></param>
    /// <param name="cancellationToken"></param>
    private async Task HandleNotificationAsync(HttpListenerContext context, string? sessionIdStr, JsonRpcNotification notification,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sessionIdStr) || !_sessions.TryGetValue(sessionIdStr, out var session))
        {
            await context.RespondHttpError(HttpStatusCode.NotFound, "Session not found");
            return;
        }
        await _manager.HandleRequestAsync(
            new JsonRpcRequest { Method = notification.Method, Params = notification.Params },
            s => s.AddTransportSession(session, Log),
            cancellationToken);
        context.RespondHttpSuccess(HttpStatusCode.Accepted);
    }

    /// <summary>
    /// JSON-RPC 请求（包含 initialize 和普通请求两种路径）。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sessionIdStr"></param>
    /// <param name="jsonRpcRequest"></param>
    /// <param name="cancellationToken"></param>
    private async Task HandleRpcRequestAsync(HttpListenerContext context, string? sessionIdStr, JsonRpcRequest jsonRpcRequest,
        CancellationToken cancellationToken)
    {
        var session = await GetOrCreateSessionAsync(context, sessionIdStr, jsonRpcRequest);
        if (session is null) return;

        if (jsonRpcRequest.Method == RequestMethods.Initialize)
        {
            await HandleInitializeAsync(context, session, jsonRpcRequest, cancellationToken);
        }
        else
        {
            await HandleSseRequestAsync(context, session, jsonRpcRequest, cancellationToken);
        }
    }

    /// <summary>
    /// 查找已有 Session 或为 initialize 请求创建新 Session。失败时向客户端写入错误响应并返回 null。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sessionIdStr"></param>
    /// <param name="jsonRpcRequest"></param>
    /// <returns></returns>
    private async Task<HttpServerTransportSession?> GetOrCreateSessionAsync(HttpListenerContext context, string? sessionIdStr, JsonRpcRequest jsonRpcRequest)
    {
        if (jsonRpcRequest.Method == RequestMethods.Initialize)
        {
            var newSessionId = _manager.MakeNewSessionId();
            var newSession = new HttpServerTransportSession(_manager, newSessionId.Id, "[McpServer][StreamableHttp]");
            if (_sessions.TryAdd(newSessionId.Id, newSession))
            {
                _manager.Add(newSession);
                context.Response.AppendHeader(SessionIdHeader, newSessionId.Id);
                return newSession;
            }
            await context.RespondHttpError(HttpStatusCode.InternalServerError, "Session ID collision");
            return null;
        }

        if (string.IsNullOrEmpty(sessionIdStr))
        {
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
            return null;
        }
        if (!_sessions.TryGetValue(sessionIdStr, out var session))
        {
            await context.RespondHttpError(HttpStatusCode.NotFound, "Session not found");
            return null;
        }
        return session;
    }

    /// <summary>
    /// initialize 请求：同步返回 application/json，无需 SSE 流。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="session"></param>
    /// <param name="jsonRpcRequest"></param>
    /// <param name="cancellationToken"></param>
    private async Task HandleInitializeAsync(HttpListenerContext context, HttpServerTransportSession session, JsonRpcRequest jsonRpcRequest,
        CancellationToken cancellationToken)
    {
        var initResponse = await _manager.HandleRequestAsync(jsonRpcRequest,
            s => s.AddTransportSession(session, Log),
            cancellationToken);

        if (initResponse != null)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            try
            {
                _manager.LogRawOut("[StreamableHttp]", $"POST/json, SessionId={session.SessionId}", initResponse);
                await _manager.WriteMessageAsync(context.Response.OutputStream, initResponse, cancellationToken);
                context.Response.SafeClose();
            }
            catch
            {
                // 忽略写入错误
            }
        }
        else
        {
            context.RespondHttpSuccess(HttpStatusCode.Accepted);
        }
    }

    /// <summary>
    /// 非 initialize 请求：以 text/event-stream 响应，服务端可在处理期间通过 SSE 流发起采样请求。
    /// 规范 §2.1 规则 6："The server MAY send JSON-RPC requests and notifications before sending
    /// the JSON-RPC response. These messages SHOULD relate to the originating client request."
    /// </summary>
    /// <param name="context"></param>
    /// <param name="session"></param>
    /// <param name="jsonRpcRequest"></param>
    /// <param name="cancellationToken"></param>
    private async Task HandleSseRequestAsync(HttpListenerContext context, HttpServerTransportSession session, JsonRpcRequest jsonRpcRequest,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";

        var output = context.Response.OutputStream;
        await output.WriteAsync(PrimeEventBytes, cancellationToken);
        await output.FlushAsync(cancellationToken);

        using var _ = session.SetRequestSseStream(output);

        var response = await _manager.HandleRequestAsync(jsonRpcRequest,
            s => s.AddTransportSession(session, Log),
            cancellationToken);

        if (response != null)
        {
            await session.WriteSseMessageAsync(output, response, cancellationToken);
        }
        context.Response.SafeClose();
    }

    private async Task HandleGetRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;

        // 协商检查
        var accept = request.Headers["Accept"];
        if (string.IsNullOrEmpty(accept) || !accept.Contains("text/event-stream"))
        {
            // 规范 §2.2.3: return HTTP 405 Method Not Allowed indicating the server does not offer an SSE stream [if not accepted]
            await context.RespondHttpError(HttpStatusCode.MethodNotAllowed, "Client must accept text/event-stream");
            return;
        }

        var sessionId = request.Headers[SessionIdHeader];
        if (string.IsNullOrEmpty(sessionId))
        {
            // 按照 MCP 协议规范 §2.5.2：若服务端要求会话 ID，收到不含 Mcp-Session-Id 的请求时应返回 400。
            // Servers that require a session ID SHOULD respond to requests without an Mcp-Session-Id header with HTTP 400 Bad Request.
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
            return;
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            // 按照 MCP 协议规范 §2.5.3：当会话 ID 过期或不存在时，返回 404。
            // The server MUST respond to requests containing that session ID with HTTP 404 Not Found.
            await context.RespondHttpError(HttpStatusCode.NotFound, "Session not found");
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";

        Log.Info($"[McpServer][StreamableHttp] SSE connection established. SessionId={sessionId}");

        try
        {
            var output = context.Response.OutputStream;
            await output.WriteAsync(PrimeEventBytes, cancellationToken);
            await output.FlushAsync(cancellationToken);

            // 定期发送 SSE 心跳，以便在客户端断开时通过写入/刷新失败尽快退出，
            // 避免仅依赖外部 cancellationToken 导致连接长期悬挂。
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(SseKeepAliveIntervalMs, cancellationToken);
                await output.WriteAsync(SseKeepAliveBytes, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            Log.Info($"[McpServer][StreamableHttp] SSE connection cancelled. SessionId={sessionId}");
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
            Log.Info($"[McpServer][StreamableHttp] SSE connection ended. SessionId={sessionId}");
        }
        catch (Exception ex)
        {
            Log.Info($"[McpServer][StreamableHttp] SSE connection ended. SessionId={sessionId}, Error={ex.Message}");
        }
        finally
        {
            context.Response.SafeClose();
        }
    }

    private async Task HandleDeleteRequestAsync(HttpListenerContext context)
    {
        var sessionId = context.Request.Headers[SessionIdHeader];
        if (!string.IsNullOrEmpty(sessionId))
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                await session.DisposeAsync();
            }
        }
        context.RespondHttpSuccess(HttpStatusCode.OK);
    }

    private string BuildLegacyMessageEndpointUri(HttpListenerRequest request, string sessionId)
    {
        var relativeEndpoint = $"{_options.SseMessageEndPoint}?sessionId={Uri.EscapeDataString(sessionId)}";
        if (request.Url is { } requestUrl)
        {
            return new Uri(requestUrl, relativeEndpoint).AbsoluteUri;
        }

        return relativeEndpoint;
    }

    private static bool ValidateOrigin(string? origin)
    {
        // 允许空 Origin (非浏览器)
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        // 允许 null Origin (特殊)
        if (origin.Equals("null", StringComparison.Ordinal))
        {
            return true;
        }

        // 只允许本机回环地址
        return origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase)
               || origin.StartsWith("http://127.0.0.1", StringComparison.Ordinal)
               || origin.StartsWith("http://[::1]", StringComparison.Ordinal);
    }
}

file static class Extensions
{
    extension(HttpListenerContext context)
    {
        internal void RespondHttpSuccess(HttpStatusCode statusCode)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.SafeClose();
        }

        internal async Task RespondHttpError(HttpStatusCode statusCode, string? message = null)
        {
            context.Response.StatusCode = (int)statusCode;
            if (!string.IsNullOrEmpty(message))
            {
                await using var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, leaveOpen: true);
                await writer.WriteAsync(message);
            }
            context.Response.SafeClose();
        }
    }

    internal static void SafeClose(this HttpListenerResponse response)
    {
        try
        {
            response.Close();
        }
        catch (HttpListenerException ex) when (ex.ErrorCode is 1229)
        {
            // 1229 (0x4CD) ERROR_CONNECTION_INVALID: An operation was attempted on a nonexistent network connection.
            // 客户端已关闭连接（超时或主动断开），客户端自己会重试，因此忽略此异常。
        }
    }
}
