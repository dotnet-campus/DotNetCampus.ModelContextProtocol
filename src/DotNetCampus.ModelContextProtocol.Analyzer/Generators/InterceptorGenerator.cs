#pragma warning disable RSEXPERIMENTAL002

using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

namespace DotNetCampus.ModelContextProtocol.Generators;

/// <summary>
/// 拦截器生成器，用于拦截 WithTool 方法调用。
/// </summary>
[Generator(LanguageNames.CSharp)]
public class InterceptorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 查找所有 WithTool 方法调用
        var withToolInvocations = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => IsWithToolInvocation(node),
            transform: static (ctx, ct) => TransformWithToolInvocation(ctx, ct))
            .Where(model => model is not null)
            .Select((model, ct) => model!);

        // 生成拦截器代码
        context.RegisterSourceOutput(withToolInvocations.Collect(), ExecuteInterceptors);
    }

    /// <summary>
    /// 判断节点是否为 WithTool 方法调用。
    /// </summary>
    private static bool IsWithToolInvocation(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "WithTool"
            }
        };
    }

    /// <summary>
    /// 转换 WithTool 调用为拦截器数据模型。
    /// </summary>
    private static WithToolInterceptorGeneratingModel? TransformWithToolInvocation(
        GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.Node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name: { } nameSyntax
                }
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
#pragma warning disable RSEXPERIMENTAL002
        var interceptableLocation = ctx.SemanticModel.GetInterceptableLocation(invocation);
#pragma warning restore RSEXPERIMENTAL002
        if (interceptableLocation is null)
        {
            return null;
        }

        // 获取创建模式参数（如果提供）
        var creationMode = McpServerToolCreationMode.Singleton;
        if (invocationOperation.Arguments.Length > 1)
        {
            // 尝试从第二个参数获取枚举值
            if (invocationOperation.Arguments[1].Value is { ConstantValue.HasValue: true } constantValue
                && constantValue.ConstantValue.Value is int modeValue)
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

    /// <summary>
    /// 执行拦截器代码生成。
    /// </summary>
    private void ExecuteInterceptors(
        SourceProductionContext context,
        ImmutableArray<WithToolInterceptorGeneratingModel> models)
    {
        if (models.IsEmpty)
        {
            return;
        }

        // 按工具类型分组
        var modelGroups = models
            .GroupBy(m => m.ToolType, SymbolEqualityComparer.Default)
            .ToList();

        foreach (var group in modelGroups)
        {
            var code = GenerateInterceptorCode(group.ToList());
            var firstModel = group.First();
            var fileName = $"ModelContextProtocol.Interceptors/{firstModel.ToolType.ToDisplayString()}.g.cs";
            context.AddSource(fileName, code);
        }
    }

    /// <summary>
    /// 生成拦截器代码。
    /// </summary>
    private string GenerateInterceptorCode(List<WithToolInterceptorGeneratingModel> models)
    {
        var firstModel = models[0];

        using var builder = new SourceTextBuilder()
        {
            UseFileScopedNamespace = false
        };

        builder
            .AddNamespaceDeclaration("DotNetCampus.ModelContextProtocol.Compiler", n => n
                .AddTypeDeclaration("file static class McpServerToolInterceptors", t =>
                {
                    foreach (var model in models)
                    {
                        t.AddInterceptorMethod(model);
                    }
                }))
            .AddNamespaceDeclaration("System.Runtime.CompilerServices", n => n
                .AddTypeDeclaration("file sealed class InterceptsLocationAttribute : global::System.Attribute", t => t
                    .AddAttribute("""[global::System.Diagnostics.Conditional("FOR_SOURCE_GENERATION_ONLY")]""")
                    .AddAttribute("[global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]")
                    .AddRawText("""
                        public InterceptsLocationAttribute(int version, string data)
                        {
                            _ = version;
                            _ = data;
                        }
                        """)
                ));

        return builder.ToString();
    }
}

file static class Extensions
{
    /// <summary>
    /// 添加拦截器方法。
    /// </summary>
    public static IAllowMemberDeclaration AddInterceptorMethod(
        this IAllowMemberDeclaration builder,
        WithToolInterceptorGeneratingModel model)
    {
        var toolType = model.ToolType;
        
        // 使用简化的方法名，移除特殊字符
        var simplifiedTypeName = toolType.Name.Replace("<", "_").Replace(">", "_").Replace(",", "_");

        var signature = $"""
            [global::System.Runtime.CompilerServices.InterceptsLocation({model.InterceptableLocation.Version}, "{model.InterceptableLocation.Data}")] // {model.InterceptableLocation.GetDisplayLocation()}
            public static void WithTool_{simplifiedTypeName}(
                this global::DotNetCampus.ModelContextProtocol.Servers.McpServerToolsBuilder builder,
                global::System.Func<{toolType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}> toolFactory,
                global::DotNetCampus.ModelContextProtocol.Servers.McpServerToolCreationMode creationMode = global::DotNetCampus.ModelContextProtocol.Servers.McpServerToolCreationMode.Singleton)
            """;

        return builder.AddMethodDeclaration(signature, m =>
        {
            m.WithSummaryComment($"拦截 <see cref=\"global::DotNetCampus.ModelContextProtocol.Servers.McpServerToolsBuilder.WithTool{{{toolType.ToDisplayString()}}}\"/> 方法调用。");
            
            // 为每个工具方法创建桥接实例
            foreach (var toolModel in model.ToolModels)
            {
                var bridgeTypeName = toolModel.GetBridgeTypeName();
                m.AddRawStatements(
                    $"// 为 {toolModel.Method.Name} 方法创建桥接工具",
                    $"var tool_{toolModel.Method.Name} = new global::{toolModel.Namespace}.{bridgeTypeName}(toolFactory);",
                    $"builder.WithTool<{toolType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>(tool_{toolModel.Method.Name});"
                );
            }
        });
    }

    /// <summary>
    /// 生成 InterceptsLocation 特性代码。
    /// </summary>
    private static string GenerateInterceptsLocationAttribute(WithToolInterceptorGeneratingModel model)
    {
        var location = model.InterceptableLocation;
        var displayLocation = location.GetDisplayLocation();
        return $"""[global::System.Runtime.CompilerServices.InterceptsLocation({location.Version}, "{location.Data}")] // {displayLocation}""";
    }
}
