using System.Diagnostics;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 标记在一个方法上，表示该方法实现了一个 MCP 服务器工具。
/// </summary>
[Conditional("FOR_SOURCE_GENERATION_ONLY")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class McpServerToolAttribute : Attribute
{
    /// <summary>
    /// 用于编程或逻辑使用，但在过去的规范中或作为后备用于显示名称（如果 title 不存在）。
    /// </summary>
    /// <remarks>
    /// 如果不设置，则使用方法名的 snake_case 形式作为工具名称。
    /// </remarks>
    public string? Name { get; init; }

    /// <summary>
    /// 用于 UI 和最终用户上下文 — 优化为可读并易于理解，即使对不熟悉特定领域术语的人也是如此。<br/>
    /// 如果未提供，应使用 name 作为显示名称（针对 Tool，如果存在 annotations.title，应优先使用它而不是 name）。
    /// </summary>
    /// <remarks>
    /// 如果希望实现本地化，请将其设置为本地化键名，并在 <see cref="McpServerBuilder"/> 中配置本地化转换器。
    /// </remarks>
    public string? Title { get; init; }

    /// <summary>
    /// 该工具的作用描述。<br/>
    /// 客户端可以使用这些信息来改善 LLM 对可用工具的理解。<br/>
    /// 可以将其视为给模型的"提示"。<br/>
    /// 如果没有设置，源生成器将尝试从 XML 文档注释中提取描述信息。
    /// </summary>
    /// <remarks>
    /// 如果希望实现本地化，请将其设置为本地化键名，并在 <see cref="McpServerBuilder"/> 中配置本地化转换器。
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// 如果为 true，则工具可能对其环境执行破坏性更新。<br/>
    /// 如果为 false，则工具仅执行递增更新。<br/>
    /// （此属性仅在 ReadOnly == false 时才有意义）<br/>
    /// 默认值：false
    /// </summary>
    /// <remarks>
    /// 设为 <see langword="true"/> 表示该工具可能会对其环境执行破坏性更新。这对修改环境的工具尤为重要。
    /// </remarks>
    public bool Destructive { get; init; } = false;

    /// <summary>
    /// 如果为 true，则工具是幂等的（多次调用相同参数产生相同结果）。<br/>
    /// 默认值：false
    /// </summary>
    /// <remarks>
    /// 设为 <see langword="true"/> 表示该工具在多次调用时会产生相同的效果（假设输入参数相同）。这对修改环境的工具尤为重要。
    /// </remarks>
    public bool Idempotent { get; init; } = false;

    /// <summary>
    /// 如果为 true，则此工具可能与外部实体的"开放世界"交互。<br/>
    /// 如果为 false，则工具的交互域是封闭的。<br/>
    /// 例如，网络搜索工具的世界是开放的，而内存工具的世界不是。<br/>
    /// 默认值：true
    /// </summary>
    public bool OpenWorld { get; init; }

    /// <summary>
    /// 如果为 true，则工具不会修改其环境。<br/>
    /// 默认值：false
    /// </summary>
    /// <remarks>
    /// 当该值为 <see langword="true"/> 时，<see cref="Destructive"/> 和 <see cref="Idempotent"/> 会被忽略。
    /// </remarks>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// 控制是否为此工具生成结构化输出（outputSchema + structuredContent）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 此属性采用三态逻辑：
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// 未设置（默认）：对于非空对象类型自动生成结构化输出；
    /// 对于可空对象或对象集合类型则产生编译错误（必须显式设置）。
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>true</c>：显式启用结构化输出。仅对非空对象类型有效；
    /// 对于无法生成合规 outputSchema 的类型将产生编译错误。
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>false</c>：显式禁用结构化输出。对所有类型有效。
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    public bool Structured { get; init; }
}
