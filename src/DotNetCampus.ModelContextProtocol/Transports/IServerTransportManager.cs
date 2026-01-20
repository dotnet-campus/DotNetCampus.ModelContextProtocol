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
    /// 提供给传输层调用。当传输层收到请求字符串行后，调用此方法可以将字符串读取为 JSON-RPC 请求对象。
    /// </summary>
    /// <param name="requestLine">请求字符串行。</param>
    /// <returns>读取出来的 JSON-RPC 请求对象，如果无法读取则返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 如果读取失败，此方法会暴露底层的任何读取异常，传输层需处理好此异常（说明请求消息不正确）。
    /// </remarks>
    ValueTask<JsonRpcRequest?> ReadRequestAsync(string requestLine);

    /// <summary>
    /// 提供给传输层调用。当传输层收到请求流后，调用此方法可以将请求流读取为 JSON-RPC 请求对象。
    /// </summary>
    /// <param name="requestStream">请求流。</param>
    /// <returns>读取出来的 JSON-RPC 请求对象，如果无法读取则返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 如果读取失败，此方法会暴露底层的任何读取异常，传输层需处理好此异常（说明请求消息不正确或连接关闭等）。
    /// </remarks>
    ValueTask<JsonRpcRequest?> ReadRequestAsync(Stream requestStream);

    /// <summary>
    /// 提供给传输层调用。当传输层调用 <see cref="HandleRequestAsync"/> 处理完请求并返回了响应后，调用此方法可以将响应 JSON-RPC 对象写入到流中。
    /// </summary>
    /// <param name="responseStream">响应流。</param>
    /// <param name="response">即将写入的 JSON-RPC 响应对象。</param>
    /// <param name="cancellationToken">如果需要取消写入，则传入此令牌。</param>
    /// <remarks>
    /// 如果写入失败，此方法会暴露底层的任何写入异常，传输层需处理好此异常（说明连接关闭等）。
    /// </remarks>
    ValueTask WriteResponseAsync(Stream responseStream, JsonRpcResponse response, CancellationToken cancellationToken);

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
