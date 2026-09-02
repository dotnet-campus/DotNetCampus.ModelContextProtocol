using System.Text;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.InProcess;

/// <summary>
/// In-Process 传输层的服务端会话。
/// </summary>
internal sealed class InProcessServerTransportSession : ServerTransportSession
{
    private readonly IServerTransportManager _manager;
    private readonly InProcessTransportPair _transportPair;
    private readonly IMcpLogger _logger;

    /// <summary>
    /// 初始化 <see cref="InProcessServerTransportSession"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="transportPair">In-Process 传输层连接对。</param>
    internal InProcessServerTransportSession(IServerTransportManager manager, InProcessTransportPair transportPair)
    {
        _manager = manager;
        _transportPair = transportPair;
        _logger = manager.Context.Logger;
    }

    /// <summary>
    /// In-Process 传输层是专用的，不需要会话 ID。
    /// </summary>
    public override string? SessionId => null;

    internal async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await _manager.WriteMessageAsync(memoryStream, message, cancellationToken).ConfigureAwait(false);
        var json = Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
        _manager.LogRawOut("[InProcess]", json);
        await _transportPair.SendToClientAsync(json, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task SendRequestMessageAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        _logger.Debug($"[McpServer][InProcess] Sending server-initiated request. Method={request.Method}, Id={request.Id}, SessionId={SessionId}");
        await SendMessageAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void OnResponseReceived(string id, JsonRpcResponse response)
        => _logger.Debug($"[McpServer][InProcess] Received client response for pending request. Id={id}");

    /// <inheritdoc />
    protected override void OnUnmatchedResponse(string id, JsonRpcResponse response)
        => _logger.Warn($"[McpServer][InProcess] Received unmatched client response. Id={id}");

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        CancelAllPendingRequests();
        return ValueTask.CompletedTask;
    }
}