#pragma warning disable RSEXPERIMENTAL002

using DotNetCampus.ModelContextProtocol.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// WithResource 拦截器生成所需的数据模型。
/// </summary>
/// <param name="InterceptableLocation">拦截位置信息。</param>
/// <param name="ResourceType">被拦截的资源类型。</param>
/// <param name="InvocationKind">WithResource 调用重载类型。</param>
/// <param name="ResourceModels">资源类型中所有标记了 McpServerResourceAttribute 的方法对应的模型。</param>
public record WithResourceInterceptorGeneratingModel(
    InterceptableLocation InterceptableLocation,
    INamedTypeSymbol ResourceType,
    WithFactoryInvocationKind InvocationKind,
    List<McpServerResourceGeneratingModel> ResourceModels)
{
    /// <summary>
    /// 从语法上下文解析 WithResource 拦截器模型。
    /// </summary>
    public static WithResourceInterceptorGeneratingModel? Parse(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.Node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name: { } nameSyntax,
                },
            } invocation)
        {
            return null;
        }

        // 获取方法的语义信息
        if (ctx.SemanticModel.GetOperation(ctx.Node, ct) is not IInvocationOperation invocationOperation)
        {
            return null;
        }

        // 检查是否是 IMcpServerResourcesBuilder.WithResource 方法
        var targetMethod = invocationOperation.TargetMethod;
        if (targetMethod.Name != "WithResource")
        {
            return null;
        }

        // 检查是否定义在 IMcpServerResourcesBuilder 类型中
        if (targetMethod.ContainingType?.ToGlobalDisplayString() != G.IMcpServerResourcesBuilder)
        {
            return null;
        }

        // 检查是否有泛型参数 TMcpServerResourceType
        if (targetMethod.TypeArguments.Length != 1)
        {
            return null;
        }

        var resourceType = targetMethod.TypeArguments[0];
        var invocationKind = WithFactoryInvocationKindResolver.TryResolve(targetMethod, resourceType);
        if (invocationKind is null)
        {
            return null;
        }

        // 在 resourceType 中查找所有标记了 McpServerResourceAttribute 的方法
        var resourceMethods = resourceType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == typeof(McpServerResourceAttribute).FullName))
            .ToList();

        if (resourceMethods.Count == 0)
        {
            return null;
        }

        // 解析所有资源方法的模型
        var resourceModels = resourceMethods
            .Select(m => McpServerResourceGeneratingModel.TryParse(m, ct))
            .ToList();

        // 获取拦截位置
        var interceptableLocation = ctx.SemanticModel.GetInterceptableLocation(invocation);
        if (interceptableLocation is null)
        {
            return null;
        }

        return new WithResourceInterceptorGeneratingModel(
            interceptableLocation,
            (INamedTypeSymbol)resourceType,
            invocationKind.Value,
            resourceModels);
    }
}
