using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Utils;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// 提供给 MCP 客户端传输层的实现使用，用于将传输层对接到应用层。
/// </summary>
public interface IClientTransportManager
{
    /// <summary>
    /// 获取用于传输层的上下文信息。
    /// </summary>
    IClientTransportContext Context { get; }

    /// <summary>
    /// 客户端的每次请求都会生成一个 Id，使用此方法可生成确保唯一的请求 Id。
    /// </summary>
    /// <returns></returns>
    RequestId MakeNewRequestId();

    /// <summary>
    /// 提供给传输层调用。当传输层收到响应字符串行后，调用此方法可以将字符串读取为 JSON-RPC 响应对象。
    /// </summary>
    /// <param name="responseLine">响应字符串行。</param>
    /// <returns>读取出来的 JSON-RPC 响应对象，如果无法读取则返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 如果读取失败，此方法会暴露底层的任何读取异常，传输层需处理好此异常（说明响应消息不正确）。
    /// </remarks>
    ValueTask<JsonRpcResponse?> ReadResponseAsync(string responseLine);

    /// <summary>
    /// 提供给传输层调用。当传输层收到响应流后，调用此方法可以将响应流读取为 JSON-RPC 响应对象。
    /// </summary>
    /// <param name="responseStream">响应流。</param>
    /// <returns>读取出来的 JSON-RPC 响应对象，如果无法读取则返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 如果读取失败，此方法会暴露底层的任何读取异常，传输层需处理好此异常（说明响应消息不正确或连接关闭等）。
    /// </remarks>
    ValueTask<JsonRpcResponse?> ReadResponseAsync(Stream responseStream);

    /// <summary>
    /// 提供给传输层调用。用于将请求 JSON-RPC 对象转换成字符串待传输层写入。
    /// </summary>
    /// <param name="message">即将写入的 JSON-RPC 请求对象。</param>
    /// <returns>请求 JSON-RPC 对象的字符串内容。</returns>
    string WriteRequestAsync(JsonRpcMessage message);

    /// <summary>
    /// 提供给传输层调用。用于将请求 JSON-RPC 对象写入到流中。
    /// </summary>
    /// <param name="requestStream">请求流。</param>
    /// <param name="message">即将写入的 JSON-RPC 请求对象。</param>
    /// <param name="cancellationToken">如果需要取消写入，则传入此令牌。</param>
    /// <remarks>
    /// 如果写入失败，此方法会暴露底层的任何写入异常，传输层需处理好此异常（说明连接关闭等）。
    /// </remarks>
    ValueTask WriteRequestAsync(Stream requestStream, JsonRpcMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// 提供给传输层调用。当传输层收到响应后，调用此方法可以将响应交给 MCP 客户端进行处理。
    /// </summary>
    /// <param name="response">从传输层解析出来的 JSON-RPC 响应。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>此方法绝对不会发生异常。</remarks>
    ValueTask HandleRespondAsync(JsonRpcResponse response, CancellationToken cancellationToken = default);
}
