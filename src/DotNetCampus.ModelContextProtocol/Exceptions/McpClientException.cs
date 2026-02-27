namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 因 MCP 客户端问题导致的异常。<br/>
/// 此异常只会在 MCP 客户端内部引发，可能导致 MCP 协议不符合预期地工作，但本身并不是 MCP 协议的一部分。
/// </summary>
public class McpClientException : ModelContextProtocolException
{
    /// <summary>
    /// 初始化 <see cref="McpClientException"/> 类的新实例。
    /// </summary>
    public McpClientException()
    {
    }

    /// <summary>
    /// 使用指定的错误消息初始化 <see cref="McpClientException"/> 类的新实例。
    /// </summary>
    /// <param name="message">描述错误的消息。</param>
    public McpClientException(string message) : base(message)
    {
    }

    /// <summary>
    /// 使用指定的错误消息和对导致此异常的内部异常的引用来初始化 <see cref="McpClientException"/> 类的新实例。
    /// </summary>
    /// <param name="message">解释异常原因的错误消息。</param>
    /// <param name="innerException">导致当前异常的异常。</param>
    public McpClientException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
