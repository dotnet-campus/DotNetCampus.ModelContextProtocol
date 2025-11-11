using DotNetCampus.ModelContextProtocol.Generators.Models;
using DotNetCampus.ModelContextProtocol.Servers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetCampus.ModelContextProtocol.Generators.ModelProviders;

internal static class InterceptorModelProvider
{
    /// <summary>
    /// 查找所有的 <see cref="McpServerToolsBuilder"/>.WithTool 方法调用，并生成对应的 <see cref="WithToolInterceptorGeneratingModel"/>。
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static IncrementalValuesProvider<WithToolInterceptorGeneratingModel> SelectWithToolProvider(this IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax
                    {
                        Name.Identifier.ValueText : "WithTool",
                    },
                },
                transform: static (ctx, ct) => WithToolInterceptorGeneratingModel.Parse(ctx, ct))
            .Where(model => model is not null)
            .Select((model, ct) => model!);
    }
}
