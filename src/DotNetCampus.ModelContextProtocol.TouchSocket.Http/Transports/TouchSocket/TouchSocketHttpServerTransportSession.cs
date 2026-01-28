using System.Text;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using TouchSocket.Http;

namespace DotNetCampus.ModelContextProtocol.Transports.TouchSocket;

/// <summary>
/// HTTP 传输层的一个会话。
/// </summary>
public class TouchSocketHttpServerTransportSession : IServerTransportSession
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, false);
    private readonly TaskCompletionSource _taskCompletionSource = new();
    private readonly HttpContext _httpContext;
    private readonly StreamWriter _writer;

    /// <summary>
    /// HTTP 传输层的一个会话。
    /// </summary>
    /// <param name="sessionId">会话 Id。</param>
    /// <param name="httpContext">HTTP 上下文。</param>
    public TouchSocketHttpServerTransportSession(string sessionId, HttpContext httpContext)
    {
        SessionId = sessionId;
        httpContext.Response.IsChunk = true;
        _httpContext = httpContext;
        _writer = new StreamWriter(httpContext.Response.CreateWriteStream(), Utf8)
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

    public Task WaitForDisconnectedAsync()
    {
        return _taskCompletionSource.Task;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _httpContext.Response.CompleteChunkAsync(CancellationToken.None);
        _taskCompletionSource.TrySetResult();
    }
}
