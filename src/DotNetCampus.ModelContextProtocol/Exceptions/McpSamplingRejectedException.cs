namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当 MCP 客户端的采样请求被用户（人工审批）拒绝时引发的异常。<br/>
/// 根据 MCP 规范，客户端实现应提供人工审批机制（human-in-the-loop），允许用户在采样请求发送给 LLM 之前拒绝它。
/// </summary>
public class McpSamplingRejectedException : McpClientException
{
    /// <summary>
    /// 初始化 <see cref="McpSamplingRejectedException"/> 类的新实例。
    /// </summary>
    /// <param name="errorCode">来自客户端的 JSON-RPC 错误码。</param>
    /// <param name="message">来自客户端的拒绝原因说明。</param>
    public McpSamplingRejectedException(int errorCode, string message)
        : base($"Sampling request was rejected: [{errorCode}] {message}")
    {
        ErrorCode = errorCode;
        RejectionMessage = message;
    }

    /// <summary>
    /// 获取来自客户端的 JSON-RPC 错误码。
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// 获取来自客户端的拒绝原因说明。
    /// </summary>
    public string RejectionMessage { get; }
}
