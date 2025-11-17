namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当 MCP 服务器收到调用 MCP 工具的请求时，如果缺少必需的类型鉴别器参数，则抛出此异常。
/// </summary>
public class McpToolMissingRequiredTypeDiscriminatorException : ModelContextProtocolException
{
    /// <summary>
    /// 初始化 <see cref="McpToolMissingRequiredTypeDiscriminatorException"/> 类的新实例。
    /// </summary>
    /// <param name="typeDiscriminatorPropertyName">缺少的类型鉴别器。</param>
    /// <param name="availableValues">可用的类型鉴别器值。</param>
    public McpToolMissingRequiredTypeDiscriminatorException(string typeDiscriminatorPropertyName, params string[] availableValues)
        : base($"Missing required type discriminator: {typeDiscriminatorPropertyName}. Available values: {string.Join(", ", availableValues)}.")
    {
    }

    /// <summary>
    /// 初始化 <see cref="McpToolMissingRequiredTypeDiscriminatorException"/> 类的新实例。
    /// </summary>
    /// <param name="innerException">导致此异常的内部异常。</param>
    /// <param name="typeDiscriminatorPropertyName">缺少的类型鉴别器。</param>
    /// <param name="availableValues">可用的类型鉴别器值。</param>
    public McpToolMissingRequiredTypeDiscriminatorException(Exception innerException, string typeDiscriminatorPropertyName, params string[] availableValues)
        : base(
            $"Missing required type discriminator: {typeDiscriminatorPropertyName}. Available values: {string.Join(", ", availableValues)}. Details: {innerException.Message}",
            innerException)
    {
    }
}
