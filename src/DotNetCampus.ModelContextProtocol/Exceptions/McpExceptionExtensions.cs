using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 提供 MCP 异常的扩展方法。
/// </summary>
internal static class McpExceptionExtensions
{
    /// <param name="response">来自 MCP 另外一端的响应。</param>
    extension(JsonRpcResponse response)
    {
        /// <summary>
        /// 如果响应包含错误，则抛出相应的 <see cref="McpClientException"/> 异常。
        /// </summary>
        /// <exception cref="McpServerException">当响应包含错误时引发此异常。</exception>
        /// <remarks>
        /// 当服务端收到来自 MCP 客户端的消息后，发现消息包含错误，则会在服务端抛出此异常。
        /// </remarks>
        internal void ThrowClientExceptionIfError()
        {
            if (response.Error != null)
            {
                throw new McpClientException($"The MCP server returned an error. Code: {response.Error.Code}, Message: {response.Error.Message}");
            }
        }

        /// <summary>
        /// 如果响应包含错误，则抛出相应的 <see cref="McpServerException"/> 异常。
        /// </summary>
        /// <exception cref="McpServerException">当响应包含错误时引发此异常。</exception>
        /// <remarks>
        /// 当客户端收到来自 MCP 服务端的消息后，发现消息包含错误，则会在客户端抛出此异常。
        /// </remarks>
        internal void ThrowServerExceptionIfError()
        {
            if (response.Error != null)
            {
                throw new McpServerException($"The MCP server returned an error. Code: {response.Error.Code}, Message: {response.Error.Message}");
            }
        }
    }
}
