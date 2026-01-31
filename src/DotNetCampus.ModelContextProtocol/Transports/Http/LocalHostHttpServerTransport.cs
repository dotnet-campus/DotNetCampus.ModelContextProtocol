using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// 仅限监听本机回环地址（localhost）的 Streamable HTTP 传输层实现。
/// </summary>
public class LocalHostHttpServerTransport : IServerTransport
{
    private const string ProtocolVersionHeader = "MCP-Protocol-Version";
    private const string SessionIdHeader = "Mcp-Session-Id";
    private static readonly ReadOnlyMemory<byte> PrimeEventBytes = ": \n\n"u8.ToArray();

    private readonly IServerTransportManager _manager;
    private readonly LocalHostHttpServerTransportOptions _options;
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<string, LocalHostHttpServerTransportSession> _sessions = new();

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
        if (!requestPath.Equals(_options.EndPoint, StringComparison.OrdinalIgnoreCase))
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
                await HandlePostRequestAsync(context, cancellationToken);
                break;
            case "GET":
                await HandleGetRequestAsync(context, cancellationToken);
                break;
            case "DELETE":
                await HandleDeleteRequestAsync(context);
                break;
            default:
                response.AddHeader("Allow", "POST, GET, DELETE, OPTIONS");
                await context.RespondHttpError(HttpStatusCode.MethodNotAllowed);
                break;
        }
    }

    private async Task HandlePostRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;

        // 协议版本检查
        var protocolVersion = request.Headers[ProtocolVersionHeader];
        if (!string.IsNullOrEmpty(protocolVersion))
        {
            // 如果比最小版本小则报错
            if (protocolVersion < ProtocolVersion.Minimum)
            {
                await context.RespondHttpError(HttpStatusCode.BadRequest, $"Unsupported protocol version. Minimum required: {ProtocolVersion.Minimum}");
                return;
            }
        }

        JsonRpcRequest? jsonRpcRequest;
        try
        {
            jsonRpcRequest = await _manager.ReadRequestAsync(request.InputStream);
        }
        catch (JsonException)
        {
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Invalid JSON");
            return;
        }

        if (jsonRpcRequest == null)
        {
            await context.RespondHttpError(HttpStatusCode.BadRequest, "Empty body");
            return;
        }

        var isInitialize = jsonRpcRequest.Method == RequestMethods.Initialize;
        var sessionIdStr = request.Headers[SessionIdHeader];
        LocalHostHttpServerTransportSession? session;

        if (isInitialize)
        {
            // 初始化请求，创建新 Session
            var newSessionId = _manager.MakeNewSessionId();
            var newSession = new LocalHostHttpServerTransportSession(_manager, newSessionId.Id);

            if (_sessions.TryAdd(newSessionId.Id, newSession))
            {
                session = newSession;
                _manager.Add(session);
                context.Response.AppendHeader(SessionIdHeader, newSessionId.Id);
            }
            else
            {
                await context.RespondHttpError(HttpStatusCode.InternalServerError, "Session ID collision");
                return;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(sessionIdStr))
            {
                await context.RespondHttpError(HttpStatusCode.BadRequest, "Missing Mcp-Session-Id header");
                return;
            }

            if (!_sessions.TryGetValue(sessionIdStr, out session))
            {
                await context.RespondHttpError(HttpStatusCode.NotFound, "Session not found");
                return;
            }
        }

        var jsonRpcResponse = await _manager.HandleRequestAsync(jsonRpcRequest, cancellationToken: cancellationToken);

        if (jsonRpcResponse != null)
        {
            // Request: Success or Failed.
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            try
            {
                await _manager.WriteMessageAsync(context.Response.OutputStream, jsonRpcResponse, cancellationToken);
                context.Response.Close();
            }
            catch
            {
                // Ignore write errors
            }
        }
        else
        {
            // Notification: No need to respond.
            context.RespondHttpSuccess(HttpStatusCode.Accepted);
        }
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
            await context.RespondHttpError(HttpStatusCode.NotFound, "Missing Mcp-Session-Id header");
            return;
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            await context.RespondHttpError(HttpStatusCode.NotFound, "Session not found");
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";

        try
        {
            var output = context.Response.OutputStream;
            await output.WriteAsync(PrimeEventBytes, cancellationToken);
            await output.FlushAsync(cancellationToken);

            await session.RunSseConnectionAsync(output, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Debug($"[McpServer][StreamableHttp] SSE connection ended. SessionId={sessionId}, Error={ex.Message}");
        }
        finally
        {
            context.Response.Close();
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
            context.Response.Close();
        }

        internal async Task RespondHttpError(HttpStatusCode statusCode, string? message = null)
        {
            context.Response.StatusCode = (int)statusCode;
            if (!string.IsNullOrEmpty(message))
            {
                await using var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, leaveOpen: true);
                await writer.WriteAsync(message);
            }
            context.Response.Close();
        }
    }
}
