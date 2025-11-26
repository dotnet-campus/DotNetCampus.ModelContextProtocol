using DotNetCampus.ModelContextProtocol.Generators.Models;
using DotNetCampus.ModelContextProtocol.Servers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetCampus.ModelContextProtocol.Generators.ModelProviders;

internal static class InterceptorModelProvider
{
    /// <param name="context">初始化上下文。</param>
    extension(IncrementalGeneratorInitializationContext context)
    {
        /// <summary>
        /// 查找所有的 <see cref="IMcpServerToolsBuilder"/>.WithTool 方法调用，并生成对应的 <see cref="WithToolInterceptorGeneratingModel"/>。
        /// </summary>
        public IncrementalValuesProvider<WithToolInterceptorGeneratingModel> SelectWithToolProvider() => context.SyntaxProvider.CreateSyntaxProvider(
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

        /// <summary>
        /// 查找所有的 <see cref="IMcpServerResourcesBuilder"/>.WithResource 方法调用，并生成对应的 <see cref="WithResourceInterceptorGeneratingModel"/>。
        /// </summary>
        public IncrementalValuesProvider<WithResourceInterceptorGeneratingModel> SelectWithResourceProvider() => context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax
                    {
                        Name.Identifier.ValueText: "WithResource",
                    },
                },
                transform: static (ctx, ct) => WithResourceInterceptorGeneratingModel.Parse(ctx, ct))
            .Where(model => model is not null)
            .Select((model, ct) => model!);
    }
}
