using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 表示 MCP 服务器资源的接口。
/// </summary>
public interface IMcpServerResource
{
    /// <summary>
    /// 获取资源在 MCP 协议中的名称。
    /// </summary>
    public string ResourceName { get; }

    /// <summary>
    /// 获取资源的 URI 或 URI 模板。<br/>
    /// 静态资源返回固定 URI（如 "test://direct/text/resource"）。<br/>
    /// 模板资源返回 URI 模板（如 "test://template/resource/{id}"）。
    /// </summary>
    public string UriTemplate { get; }

    /// <summary>
    /// 指示该资源是否为模板资源（包含参数）。
    /// </summary>
    public bool IsTemplate { get; }

    /// <summary>
    /// 资源的 MIME 类型（如 text/plain、application/json）。如果未设置，将根据资源内容自动推断。
    /// </summary>
    string? MimeType { get; }

    /// <summary>
    /// 获取资源的定义信息，这些信息将被客户端和 AI 查看，以了解资源的内容和用途。
    /// </summary>
    /// <returns>资源的定义信息（静态资源）或资源模板信息。</returns>
    object GetResourceDefinition();

    /// <summary>
    /// 读取 MCP 服务器资源的方法。
    /// </summary>
    /// <param name="context">读取资源时的上下文信息。</param>
    /// <returns>表示资源读取结果的对象。</returns>
    ValueTask<ReadResourceResult> ReadResource(IMcpServerReadResourceContext context);
}
