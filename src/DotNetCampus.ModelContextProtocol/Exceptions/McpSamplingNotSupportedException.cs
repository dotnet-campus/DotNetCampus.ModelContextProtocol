namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当连接的 MCP 客户端未声明对 Sampling 能力的支持，导致无法发起 sampling/createMessage 请求时引发的异常。<br/>
/// 此异常表示客户端在能力协商阶段未声明 <c>sampling</c> 能力，而非代码使用错误。
/// </summary>
public class McpSamplingNotSupportedException : McpClientException
{
    /// <summary>
    /// 初始化 <see cref="McpSamplingNotSupportedException"/> 类的新实例。
    /// </summary>
    public McpSamplingNotSupportedException()
        : base("当前连接的客户端未声明对 Sampling 的支持。The connected client has not declared Sampling capability.")
    {
    }
}
