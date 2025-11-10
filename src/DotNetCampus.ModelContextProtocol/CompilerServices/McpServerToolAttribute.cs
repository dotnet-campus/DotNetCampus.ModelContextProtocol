using System.Diagnostics;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

[Conditional("FOR_SOURCE_GENERATION_ONLY")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class McpServerToolAttribute : Attribute
{
    /// <summary>
    /// 设置工具名称，提供给 AI 阅读。如果不设置，则使用方法名的 snake_case 形式作为工具名称。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 设置工具的标题，提供给人类阅读。其作用为让人类能一眼得知工具的作用。
    /// </summary>
    /// <remarks>
    /// 如果希望实现本地化，请将其设置为本地化键名，并在 <see cref="McpServerBuilder"/> 中配置本地化转换器。
    /// </remarks>
    public string? Title { get; init; }

    /// <summary>
    /// 设置该工具是否可以对其环境执行破坏性更新。
    /// </summary>
    /// <remarks>
    /// 设为 <see langword="true"/> 表示该工具可能会对其环境执行破坏性更新。这对修改环境的工具尤为重要。
    /// </remarks>
    public bool Destructive { get; init; } = true;

    /// <summary>
    /// 设置该工具是否为幂等的。
    /// </summary>
    /// <remarks>
    /// 设为 <see langword="true"/> 表示该工具在多次调用时会产生相同的效果（假设输入参数相同）。这对修改环境的工具尤为重要。
    /// </remarks>
    public bool Idempotent { get; init; } = false;

    /// <summary>
    /// 设置该工具是否可以访问开放的互联网。
    /// </summary>
    /// <remarks>
    /// 设为 <see langword="true"/> 表示该工具可以访问开放的互联网。否则，该工具只能访问内部资源（如内存、本地存储等）。
    /// </remarks>
    public bool OpenWorld { get; init; }

    /// <summary>
    /// 设置改工具是否不修改其环境。
    /// </summary>
    /// <remarks>
    /// 设为 <see langword="true"/> 表示该工具不会修改其环境。<br/>
    /// 当该值为 <see langword="true"/> 时，<see cref="Destructive"/> 和 <see cref="Idempotent"/> 会被忽略。
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
