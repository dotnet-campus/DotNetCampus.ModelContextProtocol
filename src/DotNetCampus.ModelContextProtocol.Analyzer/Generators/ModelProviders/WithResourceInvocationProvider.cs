#pragma warning disable RSEXPERIMENTAL002

using DotNetCampus.ModelContextProtocol.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;

namespace DotNetCampus.ModelContextProtocol.Generators.ModelProviders;

internal static class WithResourceInvocationProvider
{
    /// <summary>
    /// 选择所有 WithResource 方法调用。
    /// </summary>
    public static IncrementalValuesProvider<WithResourceInterceptorGeneratingModel> SelectWithResourceProvider(
        this IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsWithResourceInvocation(node),
                static (context, ct) => GetWithResourceModel(context, ct))
            .Where(static m => m is not null)!;
    }

    /// <summary>
    /// 判断是否为 WithResource 方法调用。
    /// </summary>
    private static bool IsWithResourceInvocation(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Name.Identifier.Text: "WithResource"
            }
        };
    }

    /// <summary>
    /// 获取 WithResource 拦截器模型。
    /// </summary>
    private static WithResourceInterceptorGeneratingModel? GetWithResourceModel(
        GeneratorSyntaxContext context,
        CancellationToken ct)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return null;
        }

        var operation = context.SemanticModel.GetOperation(invocation, ct);
        if (operation is not IInvocationOperation invocationOperation)
        {
            return null;
        }

        var method = invocationOperation.TargetMethod;
        if (method is not
            {
                Name: "WithResource",
                ContainingType.Name: "McpServerResourcesBuilder",
                IsGenericMethod: true
            })
        {
            return null;
        }

        // 获取泛型参数（资源类型）
        var resourceType = method.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
        if (resourceType is null)
        {
            return null;
        }

        // 获取拦截位置信息
        var interceptableLocation = context.SemanticModel.GetInterceptableLocation(invocation);
        if (interceptableLocation is null)
        {
            return null;
        }

        // 获取该资源类型的所有资源方法模型
        var resourceModels = GetResourceModels(resourceType, ct);

        return new WithResourceInterceptorGeneratingModel
        {
            ResourceType = resourceType,
            InterceptableLocation = interceptableLocation,
            ResourceModels = resourceModels
        };
    }

    /// <summary>
    /// 获取资源类型的所有资源方法模型。
    /// </summary>
    private static ImmutableArray<McpServerResourceGeneratingModel> GetResourceModels(
        INamedTypeSymbol resourceType,
        CancellationToken ct)
    {
        var models = new List<McpServerResourceGeneratingModel>();

        foreach (var member in resourceType.GetMembers())
        {
            if (member is not IMethodSymbol method)
            {
                continue;
            }

            var model = McpServerResourceGeneratingModel.TryParse(method, ct);
            if (model is not null)
            {
                models.Add(model);
            }
        }

        return models.ToImmutableArray();
    }
}
