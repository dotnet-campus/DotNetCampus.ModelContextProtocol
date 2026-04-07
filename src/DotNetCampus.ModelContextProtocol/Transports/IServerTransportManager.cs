using System.Diagnostics.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Hosting.Services;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Utils;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// 提供给 MCP 服务器传输层的实现使用，用于将传输层对接到应用层。
/// </summary>
public interface IServerTransportManager
{
    /// <summary>
    /// 获取或初始化服务器名称。
    /// </summary>
    string ServerName { get; }

    /// <summary>
    /// 获取或初始化服务器版本。
    /// </summary>
    string ServerVersion { get; }

    /// <summary>
    /// 获取用于传输层的上下文信息。
    /// </summary>
    IServerTransportContext Context { get; }

    /// <summary>
    /// 对于多对一的传输层，可调用此方法为每一个建立连接的客户端创建一个唯一的 Id。
    /// </summary>
    /// <returns></returns>
    SessionId MakeNewSessionId();

    /// <summary>
    /// 提供给传输层调用。当传输层创建了一个新的会话时，调用此方法将会话注册到 MCP 服务器中。
    /// </summary>
    /// <param name="session">传输层会话。</param>
    void Add(IServerTransportSession session);

    /// <summary>
    /// 提供给传输层调用。当传输层需要获取某个会话时，调用此方法获取对应的会话实例。
    /// </summary>
    /// <param name="sessionId">会话 Id。</param>
    /// <param name="session">输出会话实例。</param>
    /// <typeparam name="T">会话类型。</typeparam>
    /// <returns>如果获取成功则返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    bool TryGetSession<T>(string sessionId, [NotNullWhen(true)] out T? session) where T : class, IServerTransportSession;

    /// <summary>
    /// 提供给传输层调用。当传输层收到一行文本消息后，调用此方法将其解析为具体的 JSON-RPC 消息类型。<br/>
    /// Available for transport implementations. Parses a single line of text into a concrete JSON-RPC message type.
    /// </summary>
    /// <param name="messageLine">消息文本行。A single line of text representing a JSON-RPC message.</param>
    /// <returns>
    /// 解析出的消息对象，实际类型为 <see cref="JsonRpcRequest"/>（有 id）、<see cref="JsonRpcNotification"/>（无 id）
    /// 或 <see cref="JsonRpcResponse"/>（无 method）之一；无法解析时返回 <see langword="null"/>。<br/>
    /// The parsed message, whose runtime type is one of <see cref="JsonRpcRequest"/> (has id),
    /// <see cref="JsonRpcNotification"/> (no id), or <see cref="JsonRpcResponse"/> (no method);
    /// <see langword="null"/> if the input cannot be parsed.
    /// </returns>
    ValueTask<JsonRpcMessage?> ReadMessageAsync(string messageLine);

    /// <summary>
    /// 提供给传输层调用。当传输层收到字节流消息后，调用此方法将其解析为具体的 JSON-RPC 消息类型。<br/>
    /// Available for transport implementations. Parses a stream into a concrete JSON-RPC message type.
    /// </summary>
    /// <param name="messageStream">消息流。A stream containing a JSON-RPC message.</param>
    /// <returns>
    /// 解析出的消息对象，实际类型为 <see cref="JsonRpcRequest"/>（有 id）、<see cref="JsonRpcNotification"/>（无 id）
    /// 或 <see cref="JsonRpcResponse"/>（无 method）之一；无法解析时返回 <see langword="null"/>。<br/>
    /// The parsed message, whose runtime type is one of <see cref="JsonRpcRequest"/> (has id),
    /// <see cref="JsonRpcNotification"/> (no id), or <see cref="JsonRpcResponse"/> (no method);
    /// <see langword="null"/> if the input cannot be parsed.
    /// </returns>
    ValueTask<JsonRpcMessage?> ReadMessageAsync(Stream messageStream);

    /// <summary>
    /// 提供给传输层调用。当传输层收到字节内存消息后，调用此方法将其解析为具体的 JSON-RPC 消息类型。<br/>
    /// Available for transport implementations. Parses a memory buffer into a concrete JSON-RPC message type.
    /// </summary>
    /// <param name="messageMemory">消息字节内存。A memory buffer containing a JSON-RPC message.</param>
    /// <returns>
    /// 解析出的消息对象，实际类型为 <see cref="JsonRpcRequest"/>（有 id）、<see cref="JsonRpcNotification"/>（无 id）
    /// 或 <see cref="JsonRpcResponse"/>（无 method）之一；无法解析时返回 <see langword="null"/>。<br/>
    /// The parsed message, whose runtime type is one of <see cref="JsonRpcRequest"/> (has id),
    /// <see cref="JsonRpcNotification"/> (no id), or <see cref="JsonRpcResponse"/> (no method);
    /// <see langword="null"/> if the input cannot be parsed.
    /// </returns>
    ValueTask<JsonRpcMessage?> ReadMessageAsync(ReadOnlyMemory<byte> messageMemory);

    /// <summary>
    /// 提供给传输层调用，用于发送消息给 MCP 客户端。
    /// <list type="bullet">
    /// <item>当传输层调用 <see cref="HandleRequestAsync"/> 处理完请求并返回了响应后，调用此方法可以将响应 JSON-RPC 对象写入到流中。</item>
    /// <item>当服务端希望发送请求给客户端时，调用此方法可以将请求 JSON-RPC 对象写入到流中。</item>
    /// <item>当服务端希望给客户端发送通知时，调用此方法可以将通知 JSON-RPC 对象写入到流中。</item>
    /// </list>
    /// </summary>
    /// <param name="stream">写入流。</param>
    /// <param name="message">即将写入的 JSON-RPC 响应对象、请求对象、通知对象。</param>
    /// <param name="cancellationToken">如果需要取消写入，则传入此令牌。</param>
    /// <remarks>
    /// 如果写入失败，此方法会暴露底层的任何写入异常，传输层需处理好此异常（说明连接关闭等）。
    /// </remarks>
    Task WriteMessageAsync(Stream stream, JsonRpcMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// 提供给传输层调用。当传输层收到请求后，调用此方法可以将请求交给 MCP 服务器进行处理。
    /// </summary>
    /// <param name="request">从传输层解析出来的 JSON-RPC 请求。</param>
    /// <param name="additionalServices">可选向此次请求的处理添加额外的服务，用于 MCP 服务器业务逻辑的依赖注入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>处理结果的响应，如果返回 <see langword="null"/> 则表示不需要包装 JSON-RPC 响应，只需要发送传输层响应或不响应。</returns>
    /// <remarks>此方法绝对不会发生异常。</remarks>
    ValueTask<JsonRpcResponse?> HandleRequestAsync(JsonRpcRequest? request,
        Action<IMcpServiceCollection>? additionalServices = null,
        CancellationToken cancellationToken = default);
}
