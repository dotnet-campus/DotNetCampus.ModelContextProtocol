#pragma warning disable RSEXPERIMENTAL002

using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using DotNetCampus.ModelContextProtocol.Generators.ModelProviders;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators;

/// <summary>
/// 拦截器生成器，用于拦截 WithTool 方法调用。
/// </summary>
[Generator(LanguageNames.CSharp)]
public class InterceptorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var withToolInvocations = context.SelectWithToolProvider();
        context.RegisterSourceOutput(withToolInvocations.Collect(), ExecuteInterceptors);
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

        // 按工具类型分组，所有对同一工具类型的拦截都生成到同一个文件
        var modelGroups = models
            .GroupBy(m => m.ToolType, SymbolEqualityComparer.Default)
            .ToDictionary(g => g.Key!, g => g.ToList(), SymbolEqualityComparer.Default);

        if (modelGroups.Count == 0)
        {
            return;
        }

        // 所有拦截器生成到同一个文件
        var code = GenerateInterceptorCode(modelGroups);
        context.AddSource("DotNetCampus.ModelContextProtocol.Interceptors/McpServerToolInterceptors.g.cs", code);
    }

    /// <summary>
    /// 生成拦截器代码。
    /// </summary>
    private string GenerateInterceptorCode(Dictionary<ISymbol, List<WithToolInterceptorGeneratingModel>> modelGroups)
    {
        using var builder = new SourceTextBuilder();

        builder
            .AddNamespaceDeclaration("DotNetCampus.ModelContextProtocol.Compiler", n => n
                .AddTypeDeclaration("file static class McpServerToolInterceptors", t =>
                {
                    // 为每个工具类型生成一个拦截方法
                    foreach (var pair in modelGroups)
                    {
                        t.AddInterceptorMethod(pair.Value);
                    }
                }))
            .AddInterceptsLocationAttributeDefinition();

        return builder.ToString();
    }
}

file static class Extensions
{
    /// <summary>
    /// 添加拦截器方法（为同一工具类型的所有拦截位置生成一个方法）。
    /// </summary>
    public static IAllowMemberDeclaration AddInterceptorMethod(
        this IAllowMemberDeclaration builder,
        List<WithToolInterceptorGeneratingModel> models)
    {
        if (models.Count == 0)
        {
            return builder;
        }

        var firstModel = models[0];
        var toolType = firstModel.ToolType;
        var simplifiedTypeName = NamingHelper.MakePascalCase(toolType.ToDisplayString());

        var signature = $"""
            public static global::DotNetCampus.ModelContextProtocol.Servers.McpServerToolsBuilder WithTool_{simplifiedTypeName}<TMcpServerToolType>(
                this global::DotNetCampus.ModelContextProtocol.Servers.McpServerToolsBuilder builder,
                global::System.Func<TMcpServerToolType> toolFactory,
                global::DotNetCampus.ModelContextProtocol.CompilerServices.CreationMode creationMode = global::DotNetCampus.ModelContextProtocol.CompilerServices.CreationMode.Singleton)
                where TMcpServerToolType : class
            """;

        return builder.AddMethodDeclaration(signature, m => m
            .WithSummaryComment($"拦截 WithTool&lt;{{{toolType.Name}}}&gt; 方法调用。")
            .AddAttributes(models.Select(GenerateInterceptsLocationAttribute))
            .AddRawStatement($$"""
                {{G.Func}}<{{toolType.ToUsingString()}}> typedFactory = creationMode switch
                {
                    {{G.CreationMode}}.Singleton => () => builder.Tools.GetOrAddSingleton<{{toolType.ToUsingString()}}>("{{toolType.ToDisplayString()}}", () => ({{toolType.ToUsingString()}})(object)toolFactory()),
                    _ => () => ({{toolType.ToUsingString()}})(object)toolFactory(),
                };
                """)
            .AddRawStatements(firstModel.ToolModels.SelectMany(toolModel => GenerateToolBridgeCreation(toolModel, toolType)))
            .AddRawStatement("return builder;")
        );
    }

    /// <summary>
    /// 添加 InterceptsLocationAttribute 定义。
    /// </summary>
    public static SourceTextBuilder AddInterceptsLocationAttributeDefinition(this SourceTextBuilder builder)
    {
        builder.AddNamespaceDeclaration("System.Runtime.CompilerServices", n => n
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
        return builder;
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

    /// <summary>
    /// 生成工具桥接实例创建代码。
    /// </summary>
    private static IEnumerable<string> GenerateToolBridgeCreation(McpServerToolGeneratingModel toolModel, INamedTypeSymbol toolType)
    {
        var bridgeTypeName = toolModel.GetBridgeTypeName();
        yield return $$"""
            // 为 {{toolModel.Method.Name}} 方法创建桥接工具
            var tool_{{toolModel.Method.Name}} = new global::{{toolModel.Namespace}}.{{bridgeTypeName}}(typedFactory);
            builder.WithTool<TMcpServerToolType>(tool_{{toolModel.Method.Name}});
            """;
    }
}
