using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;

namespace DotNetCampus.ModelContextProtocol.Transports.TouchSocket;

/// <summary>
/// Streamable HTTP 传输层的一个会话。
/// </summary>
public class TouchSocketHttpServerTransportSession : IServerTransportSession
{
    private static readonly ReadOnlyMemory<byte> EventMessageBytes = "event: message\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> DataPrefixBytes = "data: "u8.ToArray();
    private static readonly ReadOnlyMemory<byte> NewLineBytes = "\n"u8.ToArray();

    private readonly IServerTransportManager _manager;
    private readonly Channel<JsonRpcMessage> _outgoingMessages;
    private readonly CancellationTokenSource _disposeCts = new();

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public string SessionId { get; }

    /// <summary>
    /// 初始化 <see cref="TouchSocketHttpServerTransportSession"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="sessionId">唯一标识此会话的 ID。</param>
    public TouchSocketHttpServerTransportSession(IServerTransportManager manager, string sessionId)
    {
        _manager = manager;
        SessionId = sessionId;
        _outgoingMessages = Channel.CreateUnbounded<JsonRpcMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <inheritdoc />
    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_disposeCts.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }
        return _outgoingMessages.Writer.WriteAsync(message, cancellationToken).AsTask();
    }

    /// <summary>
    /// 运行 SSE 长连接，持续向客户端推送消息，直到连接断开或取消。
    /// </summary>
    /// <param name="outputStream">用于向客户端写入 SSE 数据的输出流。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    public async Task RunSseConnectionAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        var ct = linkedCts.Token;

        try
        {
            Log.Debug($"[McpServer][TouchSocket] SSE connection started. SessionId={SessionId}");

            // Wait for messages and write them
            await foreach (var message in _outgoingMessages.Reader.ReadAllAsync(ct))
            {
                await WriteSseMessageAsync(outputStream, message, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            Log.Warn($"[McpServer][TouchSocket] SSE connection error. SessionId={SessionId}, Error={ex.Message}");
        }
        finally
        {
            Log.Debug($"[McpServer][TouchSocket] SSE connection ended. SessionId={SessionId}");
        }
    }

    private async Task WriteSseMessageAsync(Stream stream, JsonRpcMessage message, CancellationToken ct)
    {
        try
        {
            // event: message
            await stream.WriteAsync(EventMessageBytes, ct);

            // data: ...
            await stream.WriteAsync(DataPrefixBytes, ct);

            // Serialize
            await _manager.WriteMessageAsync(stream, message, ct);

            // \n\n (End of event)
            await stream.WriteAsync(NewLineBytes, ct);
            await stream.WriteAsync(NewLineBytes, ct);

            await stream.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            Log.Error($"[McpServer][TouchSocket] Failed to write SSE message. SessionId={SessionId}", ex);
            throw; // Re-throw to close connection if write fails
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposeCts.IsCancellationRequested)
        {
            return;
        }

#if NET8_0_OR_GREATER
        await _disposeCts.CancelAsync();
#else
        await Task.Yield();
        _disposeCts.Cancel();
#endif
        _outgoingMessages.Writer.TryComplete();
        _disposeCts.Dispose();
    }
}
