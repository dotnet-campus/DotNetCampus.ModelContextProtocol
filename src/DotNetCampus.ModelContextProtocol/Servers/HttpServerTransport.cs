using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using DotNetCampus.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Utils;
using McpServerRequestJsonContext = DotNetCampus.ModelContextProtocol.CompilerServices.McpServerRequestJsonContext;
using McpServerResponseJsonContext = DotNetCampus.ModelContextProtocol.CompilerServices.McpServerResponseJsonContext;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP HTTP 传输层实现
/// 支持新协议 Streamable HTTP (2025-03-26+) 和旧协议 HTTP+SSE (2024-11-05) 的兼容
/// </summary>
public class HttpServerTransport
{
    private readonly McpServerContext _context;
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<string, SseSession> _sseSessions = [];

    /// <summary>
    /// 初始化 <see cref="HttpServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="context">MCP 服务器上下文</param>
    /// <param name="options">HTTP 传输层选项</param>
    public HttpServerTransport(McpServerContext context, HttpServerTransportOptions options)
    {
        _context = context;

        foreach (var prefix in options.UrlPrefixes)
        {
            _listener.Prefixes.Add(prefix.EndsWith('/') ? prefix : prefix + '/');
        }

        EndPoint = options.Endpoint.StartsWith('/') ? options.Endpoint : "/" + options.Endpoint;
    }

    private ILogger Log => _context.Logger;

    /// <summary>
    /// MCP endpoint - 用于新协议 Streamable HTTP (2025-03-26+)
    /// </summary>
    public string EndPoint { get; init; }

    /// <summary>
    /// SSE endpoint - 用于旧协议 HTTP+SSE (2024-11-05) 兼容
    /// </summary>
    private string LegacySsePath => $"{EndPoint}/sse";

    /// <summary>
    /// Message endpoint - 用于旧协议 HTTP+SSE (2024-11-05) 兼容
    /// </summary>
    private string LegacyMessagePath => $"{EndPoint}/messages";

    /// <summary>
    /// 启动 HTTP 服务器并开始监听请求。
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listener.Start();

        Log.Info($"[McpServer][Http] listening on {string.Join(", ", _listener.Prefixes)}");
        Log.Info($"[McpServer][Http] MCP Endpoint: {EndPoint}");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                Log.Trace($"[McpServer][Http] Received request: {ctx.Request.HttpMethod} {ctx.Request.Url}");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleRequestAsync(ctx);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[McpServer][Http] Unhandled exception in HandleRequestAsync", ex);
                        try
                        {
                            RespondWithError(ctx, HttpStatusCode.InternalServerError);
                        }
                        catch
                        {
                            // 忽略响应失败（可能连接已关闭）
                        }
                    }
                }, cancellationToken);
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995) // ERROR_OPERATION_ABORTED
            {
                // 正常关闭
                Log.Info($"[McpServer][Http] Listener stopped");
                break;
            }
            catch (Exception ex)
            {
                Log.Error($"[McpServer][Http] Error in GetContextAsync", ex);
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        var endpoint = ctx.Request.Url?.AbsolutePath;
        if (endpoint is null)
        {
            Log.Warn($"[McpServer][Http] Request with null URL from {ctx.Request.RemoteEndPoint}");
            RespondWithError(ctx, HttpStatusCode.NotFound);
            return;
        }

        Log.Debug($"[McpServer][Http] {ctx.Request.HttpMethod} {endpoint} from {ctx.Request.RemoteEndPoint}");

        SetCorsHeaders(ctx.Response);

        if (ctx.Request.HttpMethod == "OPTIONS")
        {
            RespondWithSuccess(ctx, HttpStatusCode.OK);
            return;
        }

        var method = ctx.Request.HttpMethod;

        // 新协议 (2025-03-26+): Streamable HTTP
        // 参考: https://modelcontextprotocol.io/specification/2025-03-26/basic/transports
        if (method == "GET" && endpoint.Equals(EndPoint, StringComparison.OrdinalIgnoreCase))
        {
            await HandleSseConnectionAsync(ctx);
            return;
        }

        if (method == "POST" && endpoint.Equals(EndPoint, StringComparison.OrdinalIgnoreCase))
        {
            await HandleJsonRpcRequestAsync(ctx);
            return;
        }

        if (method == "DELETE" && endpoint.Equals(EndPoint, StringComparison.OrdinalIgnoreCase))
        {
            await HandleDeleteSessionAsync(ctx);
            return;
        }

        // 旧协议 (2024-11-05): HTTP+SSE 兼容
        // 参考: https://modelcontextprotocol.io/specification/2024-11-05/basic/transports#http-with-sse
        if (method == "GET" && endpoint.Equals(LegacySsePath, StringComparison.OrdinalIgnoreCase))
        {
            await HandleLegacySseConnectionAsync(ctx);
            return;
        }

        if (method == "POST" && endpoint.Equals(LegacyMessagePath, StringComparison.OrdinalIgnoreCase))
        {
            await HandleLegacyMessageRequestAsync(ctx);
            return;
        }

        Log.Warn($"[McpServer][Http] No handler found for {method} {endpoint}");
        RespondWithError(ctx, HttpStatusCode.NotFound);
    }

    #region 新协议实现 (Streamable HTTP - 2025-03-26+)

    /// <summary>
    /// 处理 SSE 连接 (新协议)
    /// 参考: https://modelcontextprotocol.io/specification/2025-03-26/basic/transports#listening-for-messages-from-the-server
    /// </summary>
    private async Task HandleSseConnectionAsync(HttpListenerContext context)
    {
        var sessionId = SessionId.MakeNew().Id;

        SetSseResponseHeaders(context.Response);

        var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8) { AutoFlush = true };
        var session = new SseSession(writer, new CancellationTokenSource());

        if (!_sseSessions.TryAdd(sessionId, session))
        {
            throw new UnreachableException($"Session ID collision: '{sessionId}'");
        }

        Log.Info($"[McpServer][Http] SSE connection established: {sessionId}");

        try
        {
            // 新协议不发送 endpoint 事件，直接保持连接用于服务器推送
            await Task.Delay(Timeout.Infinite, session.CancellationToken.Token);
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
        catch (Exception ex)
        {
            Log.Warn($"[McpServer][Http] SSE connection error: {sessionId}", ex);
        }
        finally
        {
            _sseSessions.TryRemove(sessionId, out _);
            Log.Info($"[McpServer][Http] SSE connection closed: {sessionId}");
            await writer.DisposeAsync();
        }
    }

    /// <summary>
    /// 处理 JSON-RPC 请求 (新协议)
    /// 参考: https://modelcontextprotocol.io/specification/2025-03-26/basic/transports#sending-messages-to-the-server
    /// </summary>
    private async Task HandleJsonRpcRequestAsync(HttpListenerContext ctx)
    {
        var sessionId = ctx.Request.Headers["Mcp-Session-Id"];

        try
        {
            var message = await ReadJsonRpcMessageAsync(ctx.Request.InputStream);
            if (message is null)
            {
                RespondWithError(ctx, HttpStatusCode.BadRequest, "Invalid JSON-RPC request");
                return;
            }

            // 验证 Session (initialize 请求除外)
            if (message.Method != "initialize")
            {
                if (string.IsNullOrEmpty(sessionId))
                {
                    RespondWithError(ctx, HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
                    return;
                }

                if (!_sseSessions.ContainsKey(sessionId))
                {
                    RespondWithError(ctx, HttpStatusCode.NotFound, $"Session not found: {sessionId}");
                    return;
                }
            }

            // 处理请求
            var services = new ScopedServiceProvider(_context.ServiceProvider)
                .AddHttpTransportServices(sessionId, ctx.Request.Headers);
            var response = await _context.Handlers.HandleRequestAsync(services, message, CancellationToken.None);

            // Initialize 请求：创建新会话
            if (message.Method == "initialize")
            {
                sessionId = SessionId.MakeNew().Id;
                ctx.Response.Headers.Add("Mcp-Session-Id", sessionId);

                // 创建占位 session (实际 SSE 连接可能稍后建立)
                var placeholderSession = new SseSession(null!, new CancellationTokenSource());
                _sseSessions.TryAdd(sessionId, placeholderSession);

                Log.Info($"[McpServer][Http] Session created: {sessionId}");
            }

            if (response.IsNoResponse)
            {
                // Notification：根据 MCP 协议，必须返回 202 Accepted
                // https://modelcontextprotocol.io/specification/2025-06-18/basic/transports#sending-messages-to-the-server
                RespondWithSuccess(ctx, HttpStatusCode.Accepted);
            }
            else
            {
                // Request，返回响应内容
                await RespondWithJson(ctx, response);
            }
        }
        catch (JsonException ex)
        {
            Log.Error($"[McpServer][Http] JSON parsing error", ex);
            RespondWithError(ctx, HttpStatusCode.BadRequest, "Invalid JSON");
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][Http] Request handling error", ex);
            RespondWithError(ctx, HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// 处理会话终止请求 (新协议)
    /// 参考: https://modelcontextprotocol.io/specification/2025-03-26/basic/transports#session-management
    /// </summary>
    private async Task HandleDeleteSessionAsync(HttpListenerContext ctx)
    {
        var sessionId = ctx.Request.Headers["Mcp-Session-Id"];

        if (string.IsNullOrEmpty(sessionId))
        {
            RespondWithError(ctx, HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
            return;
        }

        if (!_sseSessions.TryRemove(sessionId, out var session))
        {
            // 会话不存在，但这不算错误 - 可能已经被清理了
            Log.Info($"[McpServer][Http] DELETE request for non-existent session: {sessionId}");
            RespondWithSuccess(ctx, HttpStatusCode.OK);
            return;
        }

        // 取消 SSE 连接（如果存在）
        try
        {
            session.CancellationToken.Cancel();
            session.CancellationToken.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warn($"[McpServer][Http] Error cancelling session {sessionId}", ex);
        }

        Log.Info($"[McpServer][Http] Session terminated: {sessionId}");
        RespondWithSuccess(ctx, HttpStatusCode.OK);
    }

    #endregion

    #region 旧协议兼容 (HTTP+SSE - 2024-11-05)

    /// <summary>
    /// 处理旧协议的 SSE 连接
    /// 参考: https://modelcontextprotocol.io/specification/2024-11-05/basic/transports#http-with-sse
    /// </summary>
    private async Task HandleLegacySseConnectionAsync(HttpListenerContext context)
    {
        var sessionId = SessionId.MakeNew().Id;

        SetSseResponseHeaders(context.Response);

        var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8) { AutoFlush = true };
        var session = new SseSession(writer, new CancellationTokenSource());

        if (!_sseSessions.TryAdd(sessionId, session))
        {
            throw new UnreachableException($"Session ID collision: '{sessionId}'");
        }

        Log.Info($"[McpServer][Http][Legacy] SSE connection established: {sessionId}");

        try
        {
            // 旧协议要求：发送 endpoint 事件告知客户端消息发送地址
            await writer.WriteAsync($"id:{sessionId}\n");
            await writer.WriteAsync($"event:endpoint\n");
            await writer.WriteAsync($"data:{LegacyMessagePath}?sessionId={sessionId}\n\n");

            await Task.Delay(Timeout.Infinite, session.CancellationToken.Token);
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
        catch (Exception ex)
        {
            Log.Warn($"[McpServer][Http][Legacy] SSE connection error: {sessionId}", ex);
        }
        finally
        {
            _sseSessions.TryRemove(sessionId, out _);
            Log.Info($"[McpServer][Http][Legacy] SSE connection closed: {sessionId}");
            await writer.DisposeAsync();
        }
    }

    /// <summary>
    /// 处理旧协议的消息请求 (通过 query string 传递 sessionId)
    /// 参考: https://modelcontextprotocol.io/specification/2024-11-05/basic/transports#http-with-sse
    /// </summary>
    private async Task HandleLegacyMessageRequestAsync(HttpListenerContext ctx)
    {
        var sessionId = ctx.Request.QueryString["sessionId"];

        if (string.IsNullOrEmpty(sessionId))
        {
            RespondWithError(ctx, HttpStatusCode.BadRequest, "Missing sessionId parameter");
            return;
        }

        if (!_sseSessions.TryGetValue(sessionId, out var session))
        {
            RespondWithError(ctx, HttpStatusCode.BadRequest, $"Session not found: {sessionId}");
            return;
        }

        if (session.Writer is null)
        {
            RespondWithError(ctx, HttpStatusCode.BadRequest, $"Session has no active SSE connection: {sessionId}");
            return;
        }

        try
        {
            var message = await ReadJsonRpcMessageAsync(ctx.Request.InputStream);
            if (message is null)
            {
                RespondWithError(ctx, HttpStatusCode.BadRequest, "Invalid JSON-RPC message");
                return;
            }

            var services = new ScopedServiceProvider(_context.ServiceProvider)
                .AddHttpTransportServices(sessionId, ctx.Request.Headers);
            var response = await _context.Handlers.HandleRequestAsync(services, message, CancellationToken.None);

            // Notification，不返回内容
            // Request，返回 SSE 消息
            if (!response.IsNoResponse)
            {
                await session.Writer.WriteAsync("event:message\n");
                var responseText = JsonSerializer.Serialize(response, McpServerResponseJsonContext.Default.JsonRpcResponse);
                await session.Writer.WriteAsync($"data:{responseText}\n\n");
                RespondWithSuccess(ctx, HttpStatusCode.OK);
            }
            else
            {
                // Notification：根据 MCP 协议，必须返回 202 Accepted
                RespondWithSuccess(ctx, HttpStatusCode.Accepted);
            }
        }
        catch (JsonException ex)
        {
            Log.Error($"[McpServer][Http][Legacy] JSON parsing error for session {sessionId}", ex);
            RespondWithError(ctx, HttpStatusCode.BadRequest, "Invalid JSON");
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][Http][Legacy] Request handling error for session {sessionId}", ex);
            RespondWithError(ctx, HttpStatusCode.InternalServerError);
        }
    }

    #endregion

    #region 辅助方法

    private static void SetCorsHeaders(HttpListenerResponse response)
    {
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Mcp-Session-Id");
    }

    private static void SetSseResponseHeaders(HttpListenerResponse response)
    {
        response.ContentType = "text/event-stream";
        response.Headers.Add("Cache-Control", "no-cache,no-store");
        response.Headers.Add("Content-Encoding", "identity");
        response.Headers.Add("Connection", "keep-alive");
        response.StatusCode = (int)HttpStatusCode.OK;
    }

    private static async Task<JsonRpcRequest?> ReadJsonRpcMessageAsync(Stream inputStream)
    {
        return await JsonSerializer.DeserializeAsync(inputStream, McpServerRequestJsonContext.Default.JsonRpcRequest);
    }

    private static async Task RespondWithJson(HttpListenerContext ctx, JsonRpcResponse response)
    {
        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode = (int)HttpStatusCode.OK;

        await JsonSerializer.SerializeAsync(ctx.Response.OutputStream, response, McpServerResponseJsonContext.Default.JsonRpcResponse);
        ctx.Response.Close();
    }

    /// <summary>
    /// 返回 JSON-RPC 错误响应（HTTP 200 + JSON-RPC error）
    /// 用于已经进入 JSON-RPC 协议层的错误
    /// </summary>
    private static async Task RespondWithJsonRpcError(HttpListenerContext ctx, JsonRpcErrorCode errorCode, string message, object? requestId = null)
    {
        var errorResponse = new JsonRpcResponse
        {
            Id = requestId,
            Error = new JsonRpcError
            {
                Code = (int)errorCode,
                Message = message,
            },
        };

        await RespondWithJson(ctx, errorResponse);
    }

    private static void RespondWithSuccess(HttpListenerContext ctx, HttpStatusCode statusCode)
    {
        ctx.Response.StatusCode = (int)statusCode;
        ctx.Response.Close();
    }

    /// <summary>
    /// 返回 HTTP 错误（用于传输层错误，未进入 JSON-RPC 协议层）
    /// </summary>
    private static void RespondWithError(HttpListenerContext ctx, HttpStatusCode statusCode, string? message = null)
    {
        ctx.Response.StatusCode = (int)statusCode;

        if (!string.IsNullOrEmpty(message))
        {
            var errorBytes = Encoding.UTF8.GetBytes(message);
            ctx.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
        }

        ctx.Response.Close();
    }

    #endregion

    private readonly record struct SseSession(StreamWriter? Writer, CancellationTokenSource CancellationToken);
}

file static class Extensions
{
    internal static ScopedServiceProvider AddHttpTransportServices(this ScopedServiceProvider services,
        string? sessionId, NameValueCollection headers)
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
