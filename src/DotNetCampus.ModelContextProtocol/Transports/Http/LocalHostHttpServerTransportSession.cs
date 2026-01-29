using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// Streamable HTTP 传输层的一个会话。
/// </summary>
internal class LocalHostHttpServerTransportSession : IServerTransportSession
{
    private static readonly ReadOnlyMemory<byte> EventMessageBytes = "event: message\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> DataPrefixBytes = "data: "u8.ToArray();
    private static readonly ReadOnlyMemory<byte> NewLineBytes = "\n"u8.ToArray();

    private readonly IServerTransportManager _manager;
    private readonly Channel<JsonRpcMessage> _outgoingMessages;
    private readonly CancellationTokenSource _disposeCts = new();

    private IMcpLogger Log => _manager.Context.Logger;

    public string SessionId { get; }

    public LocalHostHttpServerTransportSession(IServerTransportManager manager, string sessionId)
    {
        _manager = manager;
        SessionId = sessionId;
        _outgoingMessages = Channel.CreateUnbounded<JsonRpcMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_disposeCts.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }
        return _outgoingMessages.Writer.WriteAsync(message, cancellationToken).AsTask();
    }

    public async Task RunSseConnectionAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        var ct = linkedCts.Token;

        try
        {
            Log.Debug($"[McpServer][StreamableHttp][{SessionId}] SSE connection started.");

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
            Log.Warn($"[McpServer][StreamableHttp][{SessionId}] SSE connection error: {ex.Message}");
        }
        finally
        {
            Log.Debug($"[McpServer][StreamableHttp][{SessionId}] SSE connection ended.");
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
            Log.Error($"[McpServer][StreamableHttp][{SessionId}] Failed to write SSE message", ex);
            throw; // Re-throw to close connection if write fails
        }
    }

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
