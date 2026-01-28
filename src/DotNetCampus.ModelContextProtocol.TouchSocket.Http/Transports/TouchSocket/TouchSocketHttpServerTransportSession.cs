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
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly StreamWriter _writer;

    /// <summary>
    /// HTTP 传输层的一个会话。
    /// </summary>
    /// <param name="sessionId">会话 Id。</param>
    /// <param name="httpContext">HTTP 上下文。</param>
    public TouchSocketHttpServerTransportSession(string sessionId, HttpContext httpContext)
    {
        SessionId = sessionId;
        _writer = new StreamWriter(httpContext.Response.CreateWriteStream(), Utf8)
        {
            AutoFlush = true,
        };
    }

    /// <inheritdoc />
    public string SessionId { get; }

    /// <summary>
    /// 当会话被关闭时，此令牌将被取消。
    /// </summary>
    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    /// <inheritdoc />
    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
#if NET8_0_OR_GREATER
        await _cancellationTokenSource.CancelAsync();
#else
        _cancellationTokenSource.Cancel();
#endif
        _cancellationTokenSource.Dispose();
    }
}
