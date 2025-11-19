using DotNetCampus.ModelContextProtocol.CompilerServices;
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
    /// 模板资源返回 URI 模板（如 "test://template/resource/{id}"）。<br/>
    /// Get the URI or URI template of the resource.<br/>
    /// Static resources return a fixed URI (e.g., "test://direct/text/resource").<br/>
    /// Template resources return a URI template (e.g., "test://template/resource/{id}").
    /// </summary>
    public string UriTemplate { get; }

    /// <summary>
    /// 指示该资源是否为模板资源（包含参数）。<br/>
    /// Indicates whether this is a template resource (with parameters).
    /// </summary>
    public bool IsTemplate { get; }

    /// <summary>
    /// 获取资源的定义信息，这些信息将被客户端和 AI 查看，以了解资源的内容和用途。<br/>
    /// Gets the definition information of the resource, which will be viewed by the client and AI
    /// to understand the content and purpose of the resource.
    /// </summary>
    /// <param name="jsonContext">JSON 序列化上下文。JSON serialization context.</param>
    /// <returns>资源的定义信息（静态资源）或资源模板信息。Resource definition (for static resources) or resource template.</returns>
    object GetResourceDefinition(CompiledSchemaJsonContext jsonContext);

    /// <summary>
    /// 读取 MCP 服务器资源的方法。<br/>
    /// Read the MCP server resource.
    /// </summary>
    /// <param name="context">读取资源时的上下文信息。Context information when reading the resource.</param>
    /// <returns>表示资源读取结果的对象。The result of the resource read operation.</returns>
    ValueTask<ReadResourceResult> ReadResource(IMcpServerReadResourceContext context);
}
