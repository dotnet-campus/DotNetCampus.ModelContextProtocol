#pragma warning disable RSEXPERIMENTAL002

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// WithResource 拦截器生成所需的数据模型。
/// </summary>
public record WithResourceInterceptorGeneratingModel
{
    /// <summary>
    /// 拦截位置信息。
    /// </summary>
    public required InterceptableLocation InterceptableLocation { get; init; }

    /// <summary>
    /// 被拦截的资源类型。
    /// </summary>
    public required INamedTypeSymbol ResourceType { get; init; }

    /// <summary>
    /// 资源类型中所有标记了 McpServerResourceAttribute 的方法对应的模型。
    /// </summary>
    public required ImmutableArray<McpServerResourceGeneratingModel> ResourceModels { get; init; }
}
