#pragma warning disable RSEXPERIMENTAL002

using DotNetCampus.ModelContextProtocol.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// WithTool 拦截器生成所需的数据模型。
/// </summary>
/// <param name="InterceptableLocation">拦截位置信息。</param>
/// <param name="ToolType">被拦截的工具类型。</param>
/// <param name="InvocationKind">WithTool 调用重载类型。</param>
/// <param name="ToolModels">工具类型中所有标记了 McpServerToolAttribute 的方法对应的模型。</param>
public record WithToolInterceptorGeneratingModel(
    InterceptableLocation InterceptableLocation,
    INamedTypeSymbol ToolType,
    WithFactoryInvocationKind InvocationKind,
    List<McpServerToolGeneratingModel> ToolModels)
{
    /// <summary>
    /// 从语法上下文解析 WithTool 拦截器模型。
    /// </summary>
    public static WithToolInterceptorGeneratingModel? Parse(GeneratorSyntaxContext ctx, CancellationToken ct)
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

        // 检查是否是 IMcpServerToolsBuilder.WithTool 方法
        var targetMethod = invocationOperation.TargetMethod;
        if (targetMethod.Name != "WithTool")
        {
            return null;
        }

        // 检查是否定义在 IMcpServerToolsBuilder 类型中
        if (targetMethod.ContainingType?.ToGlobalDisplayString() != G.IMcpServerToolsBuilder)
        {
            return null;
        }

        // 检查是否有泛型参数 TMcpServerToolType
        if (targetMethod.TypeArguments.Length != 1)
        {
            return null;
        }

        var toolType = targetMethod.TypeArguments[0];
        var invocationKind = GetInvocationKind(targetMethod, toolType);
        if (invocationKind is null)
        {
            return null;
        }

        // 在 toolType 中查找所有标记了 McpServerToolAttribute 的方法
        var toolMethods = toolType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == typeof(McpServerToolAttribute).FullName))
            .ToList();

        if (toolMethods.Count == 0)
        {
            return null;
        }

        // 解析所有工具方法的模型
        var toolModels = toolMethods
            .Select(m => McpServerToolGeneratingModel.TryParse(m, ct))
            .ToList();

        // 获取拦截位置
        var interceptableLocation = ctx.SemanticModel.GetInterceptableLocation(invocation);
        if (interceptableLocation is null)
        {
            return null;
        }

        return new WithToolInterceptorGeneratingModel(
            interceptableLocation,
            (INamedTypeSymbol)toolType,
            invocationKind.Value,
            toolModels);
    }

    private static WithFactoryInvocationKind? GetInvocationKind(IMethodSymbol targetMethod, ITypeSymbol toolType)
    {
        var parameters = targetMethod.Parameters;

        // IMcpServerToolsBuilder.WithTool<T>(CreationMode creationMode = ...)
        if (parameters.Length == 1
            && parameters[0].Type.ToGlobalDisplayString() == G.CreationMode)
        {
            return WithFactoryInvocationKind.WithoutFactory;
        }

        // IMcpServerToolsBuilder.WithTool<T>(Func<T> toolFactory, CreationMode creationMode = ...)
        if (parameters.Length == 2
            && parameters[0].Type is INamedTypeSymbol
            {
                Name: "Func",
                TypeArguments.Length: 1,
            } funcType
            && SymbolEqualityComparer.Default.Equals(funcType.TypeArguments[0], toolType)
            && parameters[1].Type.ToGlobalDisplayString() == G.CreationMode)
        {
            return WithFactoryInvocationKind.WithFactory;
        }

        return null;
    }
};
