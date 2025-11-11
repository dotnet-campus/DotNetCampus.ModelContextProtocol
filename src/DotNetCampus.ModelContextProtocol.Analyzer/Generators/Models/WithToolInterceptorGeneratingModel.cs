#pragma warning disable RSEXPERIMENTAL002

using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Servers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

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
    McpServerToolCreationMode CreationMode)
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

        // 检查是否是 McpServerToolsBuilder.WithTool 方法
        var targetMethod = invocationOperation.TargetMethod;
        if (targetMethod.Name != "WithTool")
        {
            return null;
        }

        // 检查是否定义在 McpServerToolsBuilder 类型中
        if (targetMethod.ContainingType?.ToDisplayString() != "DotNetCampus.ModelContextProtocol.Servers.McpServerToolsBuilder")
        {
            return null;
        }

        // 检查是否有泛型参数 TMcpServerToolType
        if (targetMethod.TypeArguments.Length != 1)
        {
            return null;
        }

        var toolType = targetMethod.TypeArguments[0];

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

        // 获取创建模式参数（如果提供）
        var creationMode = McpServerToolCreationMode.Singleton;
        if (invocationOperation.Arguments.Length > 1)
        {
            // 尝试从第二个参数获取枚举值
            if (invocationOperation.Arguments[1].Value is { ConstantValue: { HasValue: true, Value: int modeValue } })
            {
                creationMode = (McpServerToolCreationMode)modeValue;
            }
        }

        return new WithToolInterceptorGeneratingModel(
            interceptableLocation,
            (INamedTypeSymbol)toolType,
            toolModels,
            creationMode);
    }
};
