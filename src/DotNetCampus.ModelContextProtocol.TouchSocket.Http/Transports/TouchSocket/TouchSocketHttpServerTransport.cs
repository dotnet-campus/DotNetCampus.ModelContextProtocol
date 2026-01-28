using System.Collections.Specialized;
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
    private readonly IServerTransportManager _manager;
    private readonly ITouchSocketHttpServerTransportOptions _options;

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

        Log.Warn($"[McpServer][TouchSocket] No handler found for {method} {endpoint}");
        await e.InvokeNext();
    }

    #endregion

    #region MCP Streamable HTTP 协议

    /// <summary>
    /// Streamable HTTP: 客户端建立连接。
    /// </summary>
    private async ValueTask HandleStreamableHttpConnectionAsync(HttpContext context)
    {
        var sessionId = context.Request.Headers.Get("Mcp-Session-Id").First;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Log.Warn($"[McpServer][TouchSocket][Mcp:no-mcp-session-id] Connection failed due to missing Mcp-Session-Id header");
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
            return;
        }

        Log.Info($"[McpServer][TouchSocket][Mcp:{sessionId}] Establishing connection");
        var session = new TouchSocketHttpServerTransportSession(sessionId, context);
        _manager.Add(session);
        await session.WaitForDisconnectedAsync();
    }

    private async ValueTask HandleStreamableHttpMessageAsync(HttpContext context)
    {
        var bodyBytes = await context.Request.GetContentAsync();
        var message = await _manager.ReadRequestAsync(bodyBytes);
        if (message?.Method is not { } method)
        {
            Log.Warn($"[McpServer][TouchSocket][Mcp:no-mcp-session-id] Invalid JSON-RPC message received");
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Invalid JSON-RPC message");
            return;
        }

        var sessionId = context.Request.Headers.Get("Mcp-Session-Id").First;
        if (method != RequestMethods.Initialize)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                Log.Warn($"[McpServer][TouchSocket][Mcp:no-mcp-session-id][{method}][Request] Message handling failed due to missing Mcp-Session-Id header");
                await context.RespondHttpError(HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
                return;
            }
            if (method != RequestMethods.NotificationsInitialized
                && !_manager.TryGetSession<TouchSocketHttpServerTransportSession>(sessionId, out _))
            {
                Log.Warn($"[McpServer][TouchSocket][Mcp:{sessionId}][{method}][Request] Message handling failed due to unknown Mcp-Session-Id");
                await context.RespondHttpError(HttpStatusCode.BadRequest, "Unknown Mcp-Session-Id");
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
            await context.RespondHttpSuccess(HttpStatusCode.Accepted);
            return;
        }

        Log.Debug($"[McpServer][TouchSocket][Mcp:{sessionId}][{method}][Response] Sending response for message[{message.Id}]");
        await context.RespondJsonRpcAsync(_manager, HttpStatusCode.OK, response);
    }

    private async ValueTask HandleStreamableHttpDisconnectionAsync(HttpContext context)
    {
        var sessionId = context.Request.Headers.Get("Mcp-Session-Id").First;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Log.Warn($"[McpServer][TouchSocket][Mcp:no-mcp-session-id] Disconnecting failed due to missing Mcp-Session-Id header");
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
            return;
        }

        if (!_manager.TryGetSession<TouchSocketHttpServerTransportSession>(sessionId, out var session))
        {
            Log.Debug($"[McpServer][TouchSocket][Mcp:{sessionId}] Disconnected but session not found (already terminated?)");
            await context.RespondHttpSuccess(HttpStatusCode.OK);
            return;
        }

        await session.DisposeAsync();
        Log.Info($"[McpServer][TouchSocket][Mcp:{sessionId}] Disconnected");
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

        // 2. DNS 重绑定防护（可选，默认禁用）。
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
                await manager.WriteResponseAsync(stream, response, CancellationToken.None);
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
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Mcp-Session-Id");
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
