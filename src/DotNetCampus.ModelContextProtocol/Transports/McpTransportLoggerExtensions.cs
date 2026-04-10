using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// <see cref="IMcpLogger"/> 专为传输层原始消息进行日志记录的扩展方法。
/// </summary>
public static class McpTransportLoggerExtensions
{
    private const int TrimmedRawMessageMaxLength = 80;

    /// <param name="manager">MCP 传输层管理器。</param>
    extension(IServerTransportManager manager)
    {
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawIn(string tag, string jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpServer]", tag, "←", jsonRpcRawMessage);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawIn(string tag, JsonRpcMessage jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpServer]", tag, "←", jsonRpcRawMessage);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawIn(string tag, string channel, string jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpServer]", tag, "←", jsonRpcRawMessage, channel);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawIn(string tag, string channel, JsonRpcMessage jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpServer]", tag, "←", jsonRpcRawMessage, channel);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawOut(string tag, string jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpServer]", tag, "→", jsonRpcRawMessage);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawOut(string tag, JsonRpcMessage jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpServer]", tag, "→", jsonRpcRawMessage);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawOut(string tag, string channel, string jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpServer]", tag, "→", jsonRpcRawMessage, channel);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawOut(string tag, string channel, JsonRpcMessage jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpServer]", tag, "→", jsonRpcRawMessage, channel);
    }

    /// <param name="manager">MCP 传输层管理器。</param>
    extension(IClientTransportManager manager)
    {
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawIn(string tag, string jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpClient]", tag, "←", jsonRpcRawMessage);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawIn(string tag, JsonRpcMessage jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpClient]", tag, "←", jsonRpcRawMessage);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawIn(string tag, string channel, string jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpClient]", tag, "←", jsonRpcRawMessage, channel);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawIn(string tag, string channel, JsonRpcMessage jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpClient]", tag, "←", jsonRpcRawMessage, channel);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawOut(string tag, string jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpClient]", tag, "→", jsonRpcRawMessage);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawOut(string tag, JsonRpcMessage jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpClient]", tag, "→", jsonRpcRawMessage);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawOut(string tag, string channel, string jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpClient]", tag, "→", jsonRpcRawMessage, channel);
        /// <summary>记录传输层接收到的原始 JSON-RPC 消息。</summary>
        public void LogRawOut(string tag, string channel, JsonRpcMessage jsonRpcRawMessage) => ((IMcpTransportLogger)manager).LogRaw("[McpClient]", tag, "→", jsonRpcRawMessage, channel);
    }

    private static void LogRaw(this IMcpTransportLogger transportLogger, string role, string tag, string direction, string jsonRpcRawMessage, string? channel = null)
    {
        if (transportLogger.RawMessageLoggingDetailLevel is not McpTransportRawMessageLoggingDetailLevel.None
            && transportLogger.Logger.IsEnabled(LoggingLevel.Debug))
        {
            var trimmedMessage = transportLogger.RawMessageLoggingDetailLevel is McpTransportRawMessageLoggingDetailLevel.Full
                                 || jsonRpcRawMessage.Length <= TrimmedRawMessageMaxLength
                ? jsonRpcRawMessage
                : jsonRpcRawMessage[..TrimmedRawMessageMaxLength] + "...(trimmed)";
            var channelPart = channel != null ? $" [via {channel}]" : "";
            transportLogger.Logger.Debug($"{role}{tag} {direction}{channelPart} {trimmedMessage}");
        }
    }

    private static void LogRaw(this IMcpTransportLogger transportLogger, string role, string tag, string direction, JsonRpcMessage jsonRpcRawMessage, string? channel = null)
    {
        if (transportLogger.RawMessageLoggingDetailLevel is not McpTransportRawMessageLoggingDetailLevel.None
            && transportLogger.Logger.IsEnabled(LoggingLevel.Debug))
        {
            var json = JsonSerializer.Serialize(jsonRpcRawMessage, jsonRpcRawMessage switch
            {
                JsonRpcNotification => McpInternalJsonContext.Default.JsonRpcNotification,
                JsonRpcRequest => McpInternalJsonContext.Default.JsonRpcRequest,
                JsonRpcResponse => McpInternalJsonContext.Default.JsonRpcResponse,
                _ => throw new InvalidOperationException($"Unexpected JsonRpcMessage type: {jsonRpcRawMessage.GetType().FullName}"),
            });
            var trimmedMessage = transportLogger.RawMessageLoggingDetailLevel is McpTransportRawMessageLoggingDetailLevel.Full
                                 || json.Length <= TrimmedRawMessageMaxLength
                ? json
                : json[..TrimmedRawMessageMaxLength] + "...(trimmed)";
            var channelPart = channel != null ? $" [via {channel}]" : "";
            transportLogger.Logger.Debug($"{role}{tag} {direction}{channelPart} {trimmedMessage}");
        }
    }
}

/// <summary>
/// 提供 MCP 传输层日志记录功能的接口。实现此接口的类可以为传输层原始消息提供日志记录支持。
/// </summary>
internal interface IMcpTransportLogger
{
    /// <summary>
    /// 获取用于记录 MCP 传输层日志的 <see cref="IMcpLogger"/> 实例。
    /// </summary>
    IMcpLogger Logger { get; }

    /// <summary>
    /// 获取 MCP 传输层原始消息日志记录的详细程度。根据此属性的值，传输层可以决定是否记录原始消息日志，以及记录多少细节。
    /// </summary>
    McpTransportRawMessageLoggingDetailLevel RawMessageLoggingDetailLevel { get; }
}

/// <summary>
/// MCP 传输层原始消息日志记录的详细程度。
/// </summary>
public enum McpTransportRawMessageLoggingDetailLevel
{
    /// <summary>
    /// 不记录原始消息日志。
    /// </summary>
    None,

    /// <summary>
    /// 记录裁剪的原始消息。过长的消息会被裁剪以避免日志过大。
    /// </summary>
    Trimmed,

    /// <summary>
    /// 记录完整的原始消息。可能会导致日志过大，一般仅建议在调试时使用。
    /// </summary>
    Full,
}
