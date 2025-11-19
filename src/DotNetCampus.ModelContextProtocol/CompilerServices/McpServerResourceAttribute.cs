using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 标记方法为 MCP 资源提供者。资源用于向客户端提供可供 AI 模型使用的上下文数据。<br/>
/// Marks a method as an MCP resource provider. Resources are used to expose context data that can be used by AI models.
/// </summary>
/// <remarks>
/// 资源与工具的区别：资源提供上下文数据（如文件内容、数据库 schema），而工具执行操作（如搜索、写入）。<br/>
/// 资源支持订阅机制，当内容变化时可以通知客户端。<br/>
/// Difference between resources and tools: resources provide context data (like file contents, database schema), while tools perform actions (like search, write).<br/>
/// Resources support subscription mechanism, allowing clients to be notified when content changes.
/// </remarks>
[Conditional("FOR_SOURCE_GENERATION_ONLY")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpServerResourceAttribute : Attribute
{
    /// <summary>
    /// 初始化 <see cref="McpServerResourceAttribute"/> 类的新实例。<br/>
    /// Initializes a new instance of the <see cref="McpServerResourceAttribute"/> class.
    /// </summary>
    public McpServerResourceAttribute()
    {
    }

    /// <summary>
    /// 资源的 URI 模板（符合 RFC 6570 规范）。如果未设置，将使用方法参数列表生成默认 URI。<br/>
    /// A URI template (according to RFC 6570) that can be used to construct resource URIs. If not set, a default URI will be generated from the method's parameter list.
    /// </summary>
    /// <example>
    /// file:///{path}<br/>
    /// project://resource/{id}<br/>
    /// data://schema/{database}/{table}
    /// </example>
    [StringSyntax(StringSyntaxAttribute.Uri)]
    public string? UriTemplate { get; init; }

    /// <summary>
    /// 资源的名称，用于编程或逻辑使用。如果未设置，将使用方法名称。<br/>
    /// The name of the resource, intended for programmatic or logical use. If not set, the method name will be used.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 资源的人类可读标题，用于 UI 和最终用户上下文。如果未设置，将使用 Name 作为后备。<br/>
    /// Human-readable title of the resource for UI and end-user contexts. If not set, Name will be used as fallback.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 该工具的作用描述。<br/>
    /// 客户端可以使用这些信息来改善 LLM 对可用工具的理解。<br/>
    /// 可以将其视为给模型的"提示"。<br/>
    /// 如果没有设置，源生成器将尝试从 XML 文档注释中提取描述信息。<br/>
    /// A description of what this tool does.<br/>
    /// This can be used by clients to improve the LLM's understanding of available tools.
    /// It can be thought of like a "hint" to the model.<br/>
    /// If not set, the source generator will attempt to extract the description from XML documentation comments.
    /// </summary>
    /// <remarks>
    /// 如果希望实现本地化，请将其设置为本地化键名，并在 <see cref="McpServerBuilder"/> 中配置本地化转换器。<br/>
    /// To enable localization, set this to a localization key name
    /// and configure a localization transformer in <see cref="McpServerBuilder"/>.
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// 资源的 MIME 类型（如 text/plain、application/json）。如果未设置，将根据资源内容自动推断。<br/>
    /// The MIME type of the resource (e.g., text/plain, application/json). If not set, it will be inferred from the resource contents.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// 资源的图标来源。可以是 URL、data URI 或表情符号。<br/>
    /// The icon source for the resource. Can be a URL, data URI, or emoji.
    /// </summary>
    /// <example>
    /// https://example.com/icon.png<br/>
    /// data:image/png;base64,...<br/>
    /// </example>
    public string? IconSource { get; init; }
}
