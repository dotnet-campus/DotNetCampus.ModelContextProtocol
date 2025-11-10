using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using DotNetCampus.Logging;
using DotNetCampus.ModelContextProtocol.Core;
using DotNetCampus.ModelContextProtocol.Protocol;

namespace DotNetCampus.ModelContextProtocol.Servers;

public class HttpServerTransport
{
    private readonly McpServerContext _context;
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<string, HttpSession> _sseSessions = [];

    public HttpServerTransport(McpServerContext context, HttpServerTransportOptions options)
    {
        _context = context;
        _listener.Prefixes.Add(options.BaseUrl);
    }

    private ILogger Log => _context.Logger;

    public string EndPoint
    {
        get => field;
        init
        {
            field = value;
            SsePath = Path.Join(value, "sse");
            MessagePath = Path.Join(value, "messages");
        }
    } = "/mcp";

    private string SsePath { get; init; } = "/mcp/sse";

    private string MessagePath { get; init; } = "/mcp/messages";

    public async Task StartAsync()
    {
        _listener.Start();

        Log.Info($"[McpServer][Http] listening on {string.Join(", ", _listener.Prefixes)}");

        while (true)
        {
            var ctx = await _listener.GetContextAsync();
            _ = Task.Run(async () => await HandleRequestAsync(ctx));
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        var endpoint = ctx.Request.Url?.AbsolutePath;
        if (endpoint is null)
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
            ctx.Response.Close();
            return;
        }

        try
        {
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            ctx.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            // 处理预检请求。
            if (ctx.Request.HttpMethod == "OPTIONS")
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                ctx.Response.Close();
                return;
            }

            // 按照 MCP 协议规范实现：
            // https://modelcontextprotocol.io/specification/2025-06-18/basic/transports

            // 服务端使用 SSE（Server-Sent Events）将消息推送给客户端。
            if (ctx.Request.HttpMethod == "GET")
            {
                if (endpoint == EndPoint)
                {
                    await HandleConnectionAsync(ctx, false);
                }
                else if (endpoint == SsePath)
                {
                    await HandleConnectionAsync(ctx, true);
                }
                else
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    ctx.Response.Close();
                }
                return;
            }

            // 客户端的每次请求都发送一个新的 POST 请求到 MCP endpoint。
            if (ctx.Request.HttpMethod == "POST")
            {
                if (endpoint == EndPoint)
                {
                    await HandlePostRequestAsync(ctx);
                }
                else
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    ctx.Response.Close();
                }
                return;
            }

            // 按照 MCP 协议规范，不支持其他 HTTP 方法。
            ctx.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            ctx.Response.Close();
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][Http] Error handling request", ex);
            try
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                ctx.Response.Close();
            }
            catch
            {
                // 在处理异常时如果仍然异常，则忽略。
            }
        }
    }

    private async Task HandleConnectionAsync(HttpListenerContext context, bool isSse)
    {
        var sessionId = SessionId.MakeNew().Id;

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Add("Cache-Control", "no-cache,no-store");
        context.Response.Headers.Add("Content-Encoding", "identity");
        context.Response.Headers.Add("Connection", "keep-alive");
        context.Response.StatusCode = (int)HttpStatusCode.OK;

        var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8);
        writer.AutoFlush = true;

        var session = new HttpSession(writer, isSse, new CancellationTokenSource());
        var isAdded = _sseSessions.TryAdd(sessionId, session);
        if (!isAdded)
        {
            throw new UnreachableException($"Unreachable given good entropy! Session with ID '{sessionId}' has already been created.");
        }
        Log.Info($"[McpServer][Http] SSE client connected: {sessionId}");

        try
        {
            // 新协议(2025-06-18): GET 请求建立 SSE 连接,用于服务器主动推送消息。
            // 不需要发送 endpoint 事件(那是旧协议的特征)。
            
            // 保持连接直到客户端断开。
            await Task.Delay(Timeout.Infinite, session.CancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Log.Warn($"[McpServer][Http] SSE connection error for {sessionId}", ex);
        }
        finally
        {
            _sseSessions.TryRemove(sessionId, out _);
            Log.Info($"[McpServer][Http] SSE client disconnected: {sessionId}");
            writer.Close();
        }
    }

    private async Task HandlePostRequestAsync(HttpListenerContext ctx)
    {
        // 新协议(2025-06-18): POST 到 MCP endpoint 处理 JSON-RPC 消息
        // 检查是否有 session ID (除了 initialize 请求外都需要)
        var sessionId = ctx.Request.Headers["Mcp-Session-Id"];

        try
        {
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var request = JsonSerializer.Deserialize(body, McpServerRequestJsonContext.Default.JsonRpcRequest);
            if (request is null)
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                ctx.Response.Close();
                return;
            }

            // 如果不是 initialize 请求,需要验证 session ID
            if (request.Method != "initialize")
            {
                if (string.IsNullOrEmpty(sessionId))
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    ctx.Response.Close();
                    return;
                }
                
                // 验证 session 是否存在
                if (!_sseSessions.ContainsKey(sessionId))
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    ctx.Response.Close();
                    return;
                }
            }

            // 处理请求
            var response = await _context.Handlers.HandleRequestAsync(request, CancellationToken.None);

            // 如果是 initialize 请求,创建新 session 并返回 session ID
            if (request.Method == "initialize")
            {
                sessionId = SessionId.MakeNew().Id;
                ctx.Response.Headers.Add("Mcp-Session-Id", sessionId);
                
                // 创建一个空的 session 占位(实际的 SSE 连接可能稍后建立)
                var dummySession = new HttpSession(null!, false, new CancellationTokenSource());
                _sseSessions.TryAdd(sessionId, dummySession);
                
                Log.Info($"[McpServer][Http] New session created: {sessionId}");
            }

            // 返回 JSON 响应
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
            
            var responseText = JsonSerializer.Serialize(response, McpServerResponseJsonContext.Default.JsonRpcResponse);
            var responseBytes = Encoding.UTF8.GetBytes(responseText);
            await ctx.Response.OutputStream.WriteAsync(responseBytes);
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][Http] Error handling POST request", ex);
            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            ctx.Response.Close();
        }
    }

    private async Task HandleMessageRequestAsync(HttpListenerContext ctx)
    {
        // 旧协议兼容性: 支持通过 query string 传递 sessionId 到 /mcp/messages
        var query = ctx.Request.QueryString;
        var sessionId = query["sessionId"];
        if (string.IsNullOrEmpty(sessionId) || !_sseSessions.TryGetValue(sessionId, out var session))
        {
            const int errorCode = (int)HttpStatusCode.BadRequest;
            await ctx.Response.OutputStream.WriteErrorResponseAsync(errorCode, $"No session found for ID '{sessionId}'");
            ctx.Response.StatusCode = errorCode;
            ctx.Response.Close();
            return;
        }

        if (session.Writer is null)
        {
            const int errorCode = (int)HttpStatusCode.BadRequest;
            await ctx.Response.OutputStream.WriteErrorResponseAsync(errorCode, $"Session '{sessionId}' has no active SSE connection");
            ctx.Response.StatusCode = errorCode;
            ctx.Response.Close();
            return;
        }

        try
        {
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var request = JsonSerializer.Deserialize(body, McpServerRequestJsonContext.Default.JsonRpcRequest);
            if (request is null)
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }
            var response = await _context.Handlers.HandleRequestAsync(request, CancellationToken.None);
            await session.Writer.WriteAsync($"event: message\n");
            var responseText = JsonSerializer.Serialize(response, McpServerResponseJsonContext.Default.JsonRpcResponse);
            await session.Writer.WriteAsync($"data: {responseText}\n\n");
            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][Http] Error handling message for session {sessionId}", ex);
            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            ctx.Response.Close();
        }
    }

    private readonly record struct HttpSession(StreamWriter? Writer, bool IsSse, CancellationTokenSource CancellationTokenSource);
}
