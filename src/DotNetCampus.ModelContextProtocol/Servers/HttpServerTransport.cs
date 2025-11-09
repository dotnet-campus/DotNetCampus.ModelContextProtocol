using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using DotNetCampus.Logging;
using DotNetCampus.ModelContextProtocol.Core;

namespace DotNetCampus.ModelContextProtocol.Servers;

public class HttpServerTransport
{
    private readonly McpServerContext _context;
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<string, HttpSession> _sseSessions = [];
    private Func<JsonDocument, Task<JsonDocument>>? _messageHandler;

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
            SsePath = System.IO.Path.Join(value, "sse");
            MessagePath = System.IO.Path.Join(value, "messages");
        }
    } = "/mcp";

    private string SsePath { get; init; } = "/mcp/sse";

    private string MessagePath { get; init; } = "/mcp/messages";

    public async Task StartAsync(Func<JsonDocument, Task<JsonDocument>> handler)
    {
        _messageHandler = handler;
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
            ctx.Response.StatusCode = 404;
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
                ctx.Response.StatusCode = 200;
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
                    ctx.Response.StatusCode = 404;
                    ctx.Response.Close();
                }
                return;
            }

            // 客户端的每次请求都发送一个新的 POST 请求。
            if (ctx.Request.HttpMethod == "POST")
            {
                if (endpoint == MessagePath)
                {
                    using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                    var body = await reader.ReadToEndAsync();
                    ctx.Response.StatusCode = 500;
                    ctx.Response.Close();
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    ctx.Response.Close();
                }
                return;
            }

            // 按照 MCP 协议规范，不支持其他 HTTP 方法。
            ctx.Response.StatusCode = 405;
            ctx.Response.Close();
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][Http] Error handling request", ex);
            try
            {
                ctx.Response.StatusCode = 500;
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
        context.Response.StatusCode = 200;

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
            // 发送首条消息，提示客户端后续发送消息的端点。
            await writer.WriteAsync($"id:{sessionId}\n");
            await writer.WriteAsync($"event:endpoint\n");
            await writer.WriteAsync($"data:{EndPoint}/messages?sessionId={sessionId}\n\n");

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

    private readonly record struct HttpSession(StreamWriter Writer, bool IsSse, CancellationTokenSource CancellationTokenSource);
}
