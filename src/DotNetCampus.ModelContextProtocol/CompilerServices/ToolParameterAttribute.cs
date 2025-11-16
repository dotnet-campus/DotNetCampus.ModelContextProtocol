using System.Diagnostics;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 如果一个方法被标记为 <see cref="McpServerToolAttribute"/>，那么它的参数可选使用此属性进行标记，以指示参数的特殊含义。<br/>
/// If a method is marked with <see cref="McpServerToolAttribute"/>,
/// its parameters can optionally be marked with this attribute to indicate special meanings of the parameters.
/// </summary>
[Conditional("FOR_SOURCE_GENERATION_ONLY")]
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public class ToolParameterAttribute : Attribute
{
    /// <summary>
    /// 表示 MCP 工具参数的类型。<br/>
    /// Represents the type of an MCP tool parameter.
    /// </summary>
    public ToolParameterType Type { get; init; }

    /// <summary>
    /// 指定此参数在 MCP 工具调用 Json 对象中的属性名称。如果没有指定，则使用参数名称。<br/>
    /// Specifies the property name of this parameter in the MCP tool invocation JSON object.
    /// If not specified, the parameter name is used.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 对此参数的简短描述。<br/>
    /// 客户端可以使用这些信息来改善 LLM 对可用工具参数的理解。<br/>
    /// 可以将其视为给模型的"提示"。<br/>
    /// 如果没有设置，源生成器将尝试从 XML 文档注释中提取描述信息。<br/>
    /// A brief description of this parameter.<br/>
    /// This can be used by clients to improve the LLM's understanding of available tool parameters.
    /// It can be thought of like a "hint" to the model.<br/>
    /// If not set, the source generator will attempt to extract the description from XML documentation comments.
    /// </summary>
    /// <remarks>
    /// 如果希望实现本地化，请将其设置为本地化键名，并在 <see cref="McpServerBuilder"/> 中配置本地化转换器。<br/>
    /// To enable localization, set this to a localization key name
    /// and configure a localization transformer in <see cref="McpServerBuilder"/>.
    /// </remarks>
    public string? Description { get; init; }
}

/// <summary>
/// 表示 MCP 工具参数的类型。<br/>
/// Represents the type of an MCP tool parameter.
/// </summary>
public enum ToolParameterType
{
    /// <summary>
    /// 这是一个普通的参数，与其他普通参数一起，共同组成一个 Json 格式的工具调用输入。<br/>
    /// This is a regular parameter that, along with other regular parameters,
    /// collectively forms a JSON-formatted tool invocation input.
    /// </summary>
    /// <remarks>
    /// 这是默认值。<br/>
    /// This is the default value.
    /// </remarks>
    Parameter,

    /// <summary>
    /// 表示这个参数是整个工具调用输入的 JSON 对象。<br/>
    /// 当任何一个参数被标记为 <see cref="InputObject"/> 时，不允许其他任何参数被标记为 <see cref="Parameter"/>。<br/>
    /// Indicates that this parameter is the JSON object for the entire tool invocation input.
    /// When any parameter is marked as <see cref="InputObject"/>, no other parameters
    /// are allowed to be marked as <see cref="Parameter"/>.
    /// </summary>
    InputObject,

    /// <summary>
    /// 表示这个参数是 MCP 工具调用的上下文对象。无需显式设置，类型为 <see cref="IMcpServerCallToolContext"/> 的参数会自动被视为此类型。<br/>
    /// Indicates that this parameter is the context object for the MCP tool invocation.
    /// No explicit setting is required; parameters of type <see cref="IMcpServerCallToolContext"/>
    /// are automatically treated as this type.
    /// </summary>
    Context,

    /// <summary>
    /// 指示源生成器应该从依赖注入容器中解析此参数的值，而不是从工具调用的输入中获取。<br/>
    /// Indicates that the source generator should resolve the value of this parameter
    /// from the dependency injection container, rather than obtaining it from the tool invocation input.
    /// </summary>
    Injected,

    /// <summary>
    /// 表示这个参数是用于取消工具调用操作的取消令牌。无需显式设置，类型为 <see cref="CancellationToken"/> 的参数会自动被视为此类型。<br/>
    /// Indicates that this parameter is the cancellation token used to cancel the tool invocation operation.
    /// No explicit setting is required; parameters of type <see cref="CancellationToken"/>
    /// are automatically treated as this type.
    /// </summary>
    CancellationToken,
}
