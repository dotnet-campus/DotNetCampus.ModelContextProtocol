using System.Diagnostics;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 标记在一个方法上，表示该方法实现了一个 MCP 服务器工具。<br/>
/// Marks a method as implementing an MCP server tool.
/// </summary>
[Conditional("FOR_SOURCE_GENERATION_ONLY")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class McpServerToolAttribute : Attribute
{
    /// <summary>
    /// 用于编程或逻辑使用，但在过去的规范中或作为后备用于显示名称（如果 title 不存在）。<br/>
    /// Intended for programmatic or logical use, but used as a display name in past specs
    /// or fallback (if title isn't present).
    /// </summary>
    /// <remarks>
    /// 如果不设置，则使用方法名的 snake_case 形式作为工具名称。<br/>
    /// If not set, the method name in snake_case form is used as the tool name.
    /// </remarks>
    public string? Name { get; init; }

    /// <summary>
    /// 用于 UI 和最终用户上下文 — 优化为可读并易于理解，即使对不熟悉特定领域术语的人也是如此。<br/>
    /// 如果未提供，应使用 name 作为显示名称（针对 Tool，如果存在 annotations.title，应优先使用它而不是 name）。<br/>
    /// Intended for UI and end-user contexts — optimized to be human-readable
    /// and easily understood, even by those unfamiliar with domain-specific terminology.<br/>
    /// If not provided, the name should be used for display
    /// (except for Tool, where annotations.title should be given precedence over using name, if present).
    /// </summary>
    /// <remarks>
    /// 如果希望实现本地化，请将其设置为本地化键名，并在 <see cref="McpServerBuilder"/> 中配置本地化转换器。<br/>
    /// To enable localization, set this to a localization key name
    /// and configure a localization transformer in <see cref="McpServerBuilder"/>.
    /// </remarks>
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
    /// 如果为 true，则工具可能对其环境执行破坏性更新。<br/>
    /// 如果为 false，则工具仅执行递增更新。<br/>
    /// （此属性仅在 ReadOnly == false 时才有意义）<br/>
    /// 默认值：false<br/>
    /// If true, the tool may perform destructive updates to its environment.
    /// If false, the tool performs only additive updates.<br/>
    /// (This property is meaningful only when ReadOnly == false)<br/>
    /// Default: false
    /// </summary>
    /// <remarks>
    /// 设为 <see langword="true"/> 表示该工具可能会对其环境执行破坏性更新。这对修改环境的工具尤为重要。<br/>
    /// <see langword="true"/> indicates that the tool may perform destructive updates to its environment.
    /// </remarks>
    public bool Destructive { get; init; } = false;

    /// <summary>
    /// 如果为 true，则工具是幂等的（多次调用相同参数产生相同结果）。<br/>
    /// 默认值：false<br/>
    /// If true, the tool is idempotent
    /// (multiple calls with the same parameters produce the same result).<br/>
    /// Default: false
    /// </summary>
    /// <remarks>
    /// 设为 <see langword="true"/> 表示该工具在多次调用时会产生相同的效果（假设输入参数相同）。这对修改环境的工具尤为重要。<br/>
    /// <see langword="true"/> indicates that the tool produces the same effect
    /// when called multiple times (assuming the same input parameters).
    /// </remarks>
    public bool Idempotent { get; init; } = false;

    /// <summary>
    /// 如果为 true，则此工具可能与外部实体的"开放世界"交互。<br/>
    /// 如果为 false，则工具的交互域是封闭的。<br/>
    /// 例如，网络搜索工具的世界是开放的，而内存工具的世界不是。<br/>
    /// 默认值：true<br/>
    /// If true, this tool may interact with an "open world" of external<br/>
    /// entities. If false, the tool's domain of interaction is closed.<br/>
    /// For example, the world of a web search tool is open, whereas that<br/>
    /// of a memory tool is not.<br/>
    /// Default: true
    /// </summary>
    public bool OpenWorld { get; init; }

    /// <summary>
    /// 如果为 true，则工具不会修改其环境。<br/>
    /// 默认值：false<br/>
    /// If true, the tool does not modify its environment.<br/>
    /// Default: false
    /// </summary>
    /// <remarks>
    /// 当该值为 <see langword="true"/> 时，<see cref="Destructive"/> 和 <see cref="Idempotent"/> 会被忽略。<br/>
    /// When this is <see langword="true"/>, <see cref="Destructive"/> and <see cref="Idempotent"/> are ignored.
    /// </remarks>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// 设置该工具是否使用结构化内容输出。
    /// </summary>
    /// <remarks>
    /// 设为 <see langword="true"/> 时，改工具会生成 OutputSchema 来描述其输出内容的结构。
    /// </remarks>
    public bool UseStructuredContent { get; init; } = false;
}
