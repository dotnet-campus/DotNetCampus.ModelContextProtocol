using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Hosting.Services;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// 仅限监听本机回环地址（localhost）的 Streamable HTTP 传输层实现。
/// </summary>
public class LocalHostStreamableHttpTransport : IServerTransport
{
    private readonly IServerTransportManager _manager;
    private readonly LocalHostHttpTransportOptions _options;
    private readonly HttpListener _listener = new();

    /// <summary>
    /// 初始化 <see cref="LocalHostStreamableHttpTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="options">Streamable HTTP 传输层配置选项。</param>
    public LocalHostStreamableHttpTransport(IServerTransportManager manager, LocalHostHttpTransportOptions options)
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
    public Task<Task> StartAsync(CancellationToken cancellationToken = default)
    {
        _listener.Start();
        Log.Info($"[McpServer][StreamableHttp] listening on {string.Join(", ", _listener.Prefixes)}, endpoint: {_options.EndPoint}");

        return Task.FromResult(RunLoopAsync(cancellationToken));
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _listener.Stop();
        Log.Info($"[McpServer][StreamableHttp] stopped listening");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
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
                            // 可能连接已关闭
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
        _manager.Add(new LocalHostStreamableHttpTransportSession(sessionId, context)
        {
            Stateless = _options.Stateless,
        });
        return ValueTask.CompletedTask;
    }

    private async ValueTask HandleStreamableHttpMessageAsync(HttpListenerContext context)
    {
        var message = await _manager.ParseRequestStreamAsync(context.Request.InputStream);
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
                && !_manager.TryGetSession<LocalHostStreamableHttpTransportSession>(sessionId, out _))
            {
                Log.Warn($"[McpServer][StreamableHttp][Mcp:{sessionId}][{method}][Request] Message handling failed due to unknown Mcp-Session-Id");
                context.RespondHttpError(HttpStatusCode.BadRequest, "Unknown Mcp-Session-Id");
                return;
            }
        }
        else
        {
            sessionId = _manager.MakeNewSessionId();
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

        if (!_manager.TryGetSession<LocalHostStreamableHttpTransportSession>(sessionId, out var session))
        {
            Log.Debug($"[McpServer][StreamableHttp][Mcp:{sessionId}] Disconnected but session not found (already terminated?)");
            context.RespondHttpSuccess(HttpStatusCode.OK);
            return;
        }

        await session.DisposeAsync();
        Log.Info($"[McpServer][StreamableHttp][Mcp:{sessionId}] Disconnected");
        context.RespondHttpSuccess(HttpStatusCode.OK);
    }

    #endregion

    #region MCP SSE 协议（仅限兼容）

    private async Task HandleLegacySseConnectionAsync(HttpListenerContext context)
    {
        throw new NotImplementedException();
    }

    private async Task HandleLegacyMessageRequestAsync(HttpListenerContext context)
    {
        throw new NotImplementedException();
    }

    #endregion
}

file static class Extensions
{
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
                var errorBytes = Encoding.UTF8.GetBytes(message);
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
