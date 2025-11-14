using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// 传输层接口<br/>
/// Transport layer interface
/// </summary>
public interface ITransport : IAsyncDisposable
{
    /// <summary>
    /// 会话 ID<br/>
    /// Session ID
    /// </summary>
    string? SessionId { get; }

    /// <summary>
    /// 消息读取器<br/>
    /// Message reader
    /// </summary>
    ChannelReader<JsonRpcMessage> MessageReader { get; }

    /// <summary>
    /// 发送消息<br/>
    /// Send a message
    /// </summary>
    Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default);
}
