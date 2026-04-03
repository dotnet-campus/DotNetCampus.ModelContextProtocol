namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当 MCP 客户端的采样请求被用户（人工审批）拒绝时引发的异常。<br/>
/// 根据 MCP 规范，客户端实现应提供人工审批机制（human-in-the-loop），允许用户在采样请求发送给 LLM 之前拒绝它。<br/>
/// Exception thrown when a sampling request was rejected by the user (human-in-the-loop approval was denied).
/// Per the MCP specification, client implementations SHOULD provide a human-in-the-loop mechanism
/// that allows users to deny sampling requests before they are sent to an LLM.
/// </summary>
public class McpSamplingRejectedException : McpClientException
{
    /// <summary>
    /// 初始化 <see cref="McpSamplingRejectedException"/> 类的新实例。
    /// </summary>
    /// <param name="errorCode">来自客户端的 JSON-RPC 错误码。The JSON-RPC error code from the client.</param>
    /// <param name="message">来自客户端的拒绝原因说明。The rejection reason message from the client.</param>
    public McpSamplingRejectedException(int errorCode, string message)
        : base($"Sampling request was rejected: [{errorCode}] {message}")
    {
        ErrorCode = errorCode;
        RejectionMessage = message;
    }

    /// <summary>
    /// 获取来自客户端的 JSON-RPC 错误码。<br/>
    /// Gets the JSON-RPC error code returned by the client.
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// 获取来自客户端的拒绝原因说明。<br/>
    /// Gets the rejection reason message returned by the client.
    /// </summary>
    public string RejectionMessage { get; }
}
