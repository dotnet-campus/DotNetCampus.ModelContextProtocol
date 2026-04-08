using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Stdio;

/// <summary>
/// STDIO 传输层的一个会话。
/// </summary>
public class StdioServerTransportSession : IServerTransportSession
{
    private static readonly ReadOnlyMemory<byte> NewLineBytes = "\n"u8.ToArray();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = [];
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly IMcpLogger _logger;
    private StreamWriter? _output;

    /// <summary>
    /// 初始化 <see cref="StdioServerTransportSession"/> 类的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public StdioServerTransportSession(IMcpLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// STDIO 传输层是专用的，不需要会话 ID。
    /// </summary>
    public string? SessionId => null;

    /// <inheritdoc />
    public ClientCapabilities? ConnectedClientCapabilities { get; set; }

    /// <summary>
    /// 由 <see cref="StdioServerTransport"/> 在启动后设置输出流。
    /// </summary>
    internal void SetOutput(StreamWriter output)
    {
        _output = output;
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_output is not { } output)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_logger.IsEnabled(LoggingLevel.Debug))
            {
                using var ms = new MemoryStream();
                await JsonSerializer.SerializeAsync(ms, message, GetTypeInfo(message), cancellationToken).ConfigureAwait(false);
                var json = Encoding.UTF8.GetString(ms.ToArray());
                _logger.Debug($"[McpServer][Stdio] → {json}");
                await output.BaseStream.WriteAsync(ms.ToArray(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await JsonSerializer.SerializeAsync(output.BaseStream, message, GetTypeInfo(message), cancellationToken).ConfigureAwait(false);
            }
            await output.BaseStream.WriteAsync(NewLineBytes, cancellationToken).ConfigureAwait(false);
            await output.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Id?.ToString() is not { } id)
        {
            throw new InvalidOperationException("请求 ID 不能为 null。Request ID must not be null.");
        }

        _logger.Debug($"[McpServer][Stdio] Sending server-initiated request. Method={request.Method}, Id={id}");

        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        using var registration = cancellationToken.Register(() =>
        {
            if (_pendingRequests.TryRemove(id, out var removed))
            {
                removed.TrySetCanceled(cancellationToken);
            }
        });

        try
        {
            await SendMessageAsync(request, cancellationToken).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pendingRequests.TryRemove(id, out _);
        }
    }

    /// <inheritdoc />
    public void HandleResponseAsync(JsonRpcResponse response)
    {
        if (response.Id?.ToString() is not { } id)
        {
            return;
        }

        if (_pendingRequests.TryRemove(id, out var tcs))
        {
            _logger.Debug($"[McpServer][Stdio] Received client response for pending request. Id={id}");
            tcs.TrySetResult(response);
        }
        else
        {
            _logger.Warn($"[McpServer][Stdio] Received unmatched client response. Id={id}");
        }
    }

    /// <inheritdoc />
    public IDisposable AttachRequestSseChannel(ChannelWriter<JsonRpcMessage> writer)
    {
        // STDIO 传输层是全双工管道，不需要 per-request SSE 通道，此方法为空实现。
        return NopDisposable.Instance;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        foreach (var (_, tcs) in _pendingRequests)
        {
            tcs.TrySetCanceled();
        }
        _pendingRequests.Clear();
        return ValueTask.CompletedTask;
    }

    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo GetTypeInfo(JsonRpcMessage message) => message switch
    {
        JsonRpcResponse response => McpInternalJsonContext.Default.JsonRpcResponse,
        JsonRpcRequest request => McpInternalJsonContext.Default.JsonRpcRequest,
        JsonRpcNotification notification => McpInternalJsonContext.Default.JsonRpcNotification,
        _ => throw new ArgumentException($"不支持的消息类型：{message.GetType().FullName}."),
    };

    private sealed class NopDisposable : IDisposable
    {
        public static readonly NopDisposable Instance = new();
        public void Dispose() { }
    }
}
