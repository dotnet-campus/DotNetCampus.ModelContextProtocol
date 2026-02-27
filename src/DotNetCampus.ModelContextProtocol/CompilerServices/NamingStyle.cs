namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// MCP 命名风格设置。
/// </summary>
public readonly record struct McpNamingStyle
{
    /// <summary>
    /// 工具名称的命名风格。
    /// </summary>
    public NamingStyle Tool { get; init; }

    /// <summary>
    /// 工具参数名称的命名风格。
    /// </summary>
    public NamingStyle ToolArgument { get; init; }
}

/// <summary>
/// 命名风格。
/// </summary>
public enum NamingStyle : byte
{
    /// <summary>
    /// 按原样使用名称，不进行任何转换。
    /// </summary>
    Ordinal,

    /// <summary>
    /// PascalCase 命名法（每个单词的首字母大写）。
    /// </summary>
    PascalCase,

    /// <summary>
    /// camelCase 命名法（首字母小写，每个单词的首字母大写）。
    /// </summary>
    CamelCase,

    /// <summary>
    /// kebab-case 命名法（单词之间用连字符分隔，所有字母小写）。
    /// </summary>
    KebabCase,

    /// <summary>
    /// snake_case 命名法（单词之间用下划线分隔，所有字母小写）。
    /// </summary>
    SnakeCase,
}
