#pragma warning disable RSEXPERIMENTAL002

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// WithTool 拦截器生成所需的数据模型。
/// </summary>
/// <param name="InterceptableLocation">拦截位置信息。</param>
/// <param name="ToolType">被拦截的工具类型。</param>
/// <param name="ToolModels">工具类型中所有标记了 McpServerToolAttribute 的方法对应的模型。</param>
/// <param name="CreationMode">工具创建模式。</param>
public record WithToolInterceptorGeneratingModel(
    InterceptableLocation InterceptableLocation,
    INamedTypeSymbol ToolType,
    List<McpServerToolGeneratingModel> ToolModels,
    McpServerToolCreationMode CreationMode);

/// <summary>
/// MCP 服务器工具的创建模式（与 McpServerToolCreationMode 枚举同步）。
/// </summary>
public enum McpServerToolCreationMode
{
    /// <summary>
    /// 工具只调用创建委托一次，并在整个 McpServer 生命周期内重用该实例。
    /// </summary>
    Singleton,

    /// <summary>
    /// 每次调用工具时，都会调用创建委托，根据委托内的实现决定是复用还是新建实例。
    /// </summary>
    Transient,
}
