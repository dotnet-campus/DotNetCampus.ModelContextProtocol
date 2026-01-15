using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Stdio;

/// <summary>
/// STDIO 传输层的一个会话。
/// </summary>
public class StdioServerTransportSession : IServerTransportSession
{
    /// <summary>
    /// STDIO 传输层的一个会话。
    /// </summary>
    public StdioServerTransportSession()
    {
    }

    /// <summary>
    /// STDIO 传输层是专用的，不需要会话 ID。
    /// </summary>
    public string? SessionId => null;

    /// <inheritdoc />
    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
