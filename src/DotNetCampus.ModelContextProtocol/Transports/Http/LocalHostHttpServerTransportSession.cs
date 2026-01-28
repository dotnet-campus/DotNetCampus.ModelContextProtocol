using System.Net;
using System.Text;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// Streamable HTTP 传输层的一个会话。
/// </summary>
public class LocalHostHttpServerTransportSession : IServerTransportSession
{
    private readonly HttpListenerContext _httpContext;
    private readonly TaskCompletionSource _taskCompletionSource = new();
    private readonly StreamWriter _writer;

    /// <summary>
    /// Streamable HTTP 传输层的一个会话。
    /// </summary>
    /// <param name="sessionId">会话 Id。</param>
    /// <param name="httpContext">HTTP 上下文。</param>
    public LocalHostHttpServerTransportSession(string sessionId, HttpListenerContext httpContext)
    {
        _httpContext = httpContext;
        SessionId = sessionId;
        _writer = new StreamWriter(httpContext.Response.OutputStream, Encoding.UTF8)
        {
            AutoFlush = true,
        };
    }

    /// <inheritdoc />
    public string SessionId { get; }

    /// <inheritdoc />
    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    internal async ValueTask WaitForDisconnectedAsync()
    {
        await _taskCompletionSource.Task;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _httpContext.Response.Close();
        _taskCompletionSource.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
