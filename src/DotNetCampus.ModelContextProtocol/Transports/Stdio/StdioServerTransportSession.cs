using System.Text;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Stdio;

/// <summary>
/// STDIO 传输层的一个会话。
/// </summary>
public class StdioServerTransportSession : ServerTransportSession
{
    private static readonly ReadOnlyMemory<byte> NewLineBytes = "\n"u8.ToArray();
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
    public override string? SessionId => null;

    /// <summary>
    /// 由 <see cref="StdioServerTransport"/> 在启动后设置输出流。
    /// </summary>
    internal void SetOutput(StreamWriter output)
    {
        _output = output;
    }

    /// <inheritdoc />
    protected override async Task SendRequestMessageAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        _logger.Debug($"[McpServer][Stdio] Sending server-initiated request. Method={request.Method}, Id={request.Id}, SessionId={SessionId}");
        await SendMessageAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_output is not { } output)
        {
            throw new InvalidOperationException("STDIO 传输层尚未初始化输出流，无法发送服务端主动请求。");
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_logger.IsEnabled(LoggingLevel.Debug))
            {
                using var ms = new MemoryStream();
                await JsonSerializer.SerializeAsync(ms, message, GetTypeInfo(message), cancellationToken).ConfigureAwait(false);
                var bytes = ms.ToArray();
                var json = Encoding.UTF8.GetString(bytes);
                _logger.Debug($"[McpServer][Stdio] → {json}");
                await output.BaseStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
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
    protected override void OnResponseReceived(string id, JsonRpcResponse response)
        => _logger.Debug($"[McpServer][Stdio] Received client response for pending request. Id={id}");

    /// <inheritdoc />
    protected override void OnUnmatchedResponse(string id, JsonRpcResponse response)
        => _logger.Warn($"[McpServer][Stdio] Received unmatched client response. Id={id}");

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        CancelAllPendingRequests();
        return ValueTask.CompletedTask;
    }

    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo GetTypeInfo(JsonRpcMessage message) => message switch
    {
        JsonRpcResponse response => McpInternalJsonContext.Default.JsonRpcResponse,
        JsonRpcRequest request => McpInternalJsonContext.Default.JsonRpcRequest,
        JsonRpcNotification notification => McpInternalJsonContext.Default.JsonRpcNotification,
        _ => throw new ArgumentException($"不支持的消息类型：{message.GetType().FullName}."),
    };
}
