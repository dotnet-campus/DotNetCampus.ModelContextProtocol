using System.Diagnostics.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Hosting.Services;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// 用于管理 MCP 服务器传输层的管理器接口。
/// </summary>
public interface IServerTransportManager
{
    /// <summary>
    /// 获取用于传输层的上下文信息。
    /// </summary>
    IServerTransportContext Context { get; }

    /// <summary>
    /// 立即启动所有已注册的传输层。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>会一直等到所有的传输层已停止后异步返回。</returns>
    Task RunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 立即停止所有已注册的传输层。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>发起停止操作后异步返回。</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 对于多对一的传输层，可调用此方法为每一个建立连接的客户端创建一个唯一的 Id。
    /// </summary>
    /// <returns></returns>
    string MakeNewSessionId();

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
    /// 提供给传输层调用。当传输层收到请求字符串行后，调用此方法可以将请求流解析为 JSON-RPC 请求对象。
    /// </summary>
    /// <param name="inputMessageText">请求字符串行。</param>
    /// <returns>解析出来的 JSON-RPC 请求对象，如果无法解析则返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 如果解析失败，此方法会暴露底层的任何解析异常，传输层需处理好此异常（说明请求消息不正确）。
    /// </remarks>
    ValueTask<JsonRpcRequest?> ParseRequestAsync(string inputMessageText);

    /// <summary>
    /// 提供给传输层调用。当传输层收到请求流后，调用此方法可以将请求流解析为 JSON-RPC 请求对象。
    /// </summary>
    /// <param name="inputStream">请求流。</param>
    /// <returns>解析出来的 JSON-RPC 请求对象，如果无法解析则返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 如果解析失败，此方法会暴露底层的任何解析异常，传输层需处理好此异常（说明请求消息不正确或连接关闭等）。
    /// </remarks>
    ValueTask<JsonRpcRequest?> ParseRequestAsync(Stream inputStream);

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
