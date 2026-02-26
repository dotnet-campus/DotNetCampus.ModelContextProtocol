#pragma warning disable RSEXPERIMENTAL002

using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using DotNetCampus.ModelContextProtocol.Generators.ModelProviders;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators;

/// <summary>
/// 拦截器生成器，用于拦截 WithResource 方法调用。
/// </summary>
[Generator(LanguageNames.CSharp)]
public class WithResourceInterceptorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var withResourceInvocations = context.SelectWithResourceProvider();
        context.RegisterSourceOutput(withResourceInvocations.Collect(), ExecuteInterceptors);
    }

    /// <summary>
    /// 执行拦截器代码生成。
    /// </summary>
    private void ExecuteInterceptors(
        SourceProductionContext context,
        ImmutableArray<WithResourceInterceptorGeneratingModel> models)
    {
        if (models.IsEmpty)
        {
            return;
        }

        // 按资源类型分组，所有对同一资源类型的拦截都生成到同一个文件
        var modelGroups = models
            .GroupBy(m => m.ResourceType, SymbolEqualityComparer.Default)
            .ToDictionary(g => g.Key!, g => g.ToList(), SymbolEqualityComparer.Default);

        if (modelGroups.Count == 0)
        {
            return;
        }

        // 所有拦截器生成到同一个文件
        var code = GenerateInterceptorCode(modelGroups);
        context.AddSource("DotNetCampus.ModelContextProtocol.Interceptors/McpServerResourceInterceptors.g.cs", code);
    }

    /// <summary>
    /// 生成拦截器代码。
    /// </summary>
    private string GenerateInterceptorCode(Dictionary<ISymbol, List<WithResourceInterceptorGeneratingModel>> modelGroups)
    {
        using var builder = new SourceTextBuilder
            {
                RemoveIndentForPreprocessorLines = true,
            }
            .AddNamespaceDeclaration("DotNetCampus.ModelContextProtocol.Compiler", n => n
                .AddTypeDeclaration("file static class McpServerResourceInterceptors", t =>
                {
                    // 为每个资源类型生成一个拦截方法
                    foreach (var pair in modelGroups)
                    {
                        t.AddResourceInterceptorMethod(pair.Value);
                    }
                }))
            .AddInterceptsLocationAttributeDefinition();

        return builder.ToString();
    }
}

file static class Extensions
{
    /// <summary>
    /// 添加拦截器方法（为同一资源类型的所有拦截位置生成一个方法）。
    /// </summary>
    public static IAllowMemberDeclaration AddResourceInterceptorMethod(
        this IAllowMemberDeclaration builder,
        List<WithResourceInterceptorGeneratingModel> models)
    {
        if (models.Count == 0)
        {
            return builder;
        }

        var firstModel = models[0];
        var resourceType = firstModel.ResourceType;
        var simplifiedTypeName = NamingHelper.MakePascalCase(resourceType.ToDisplayString());

        var signature = $"""
            public static {G.IMcpServerResourcesBuilder} WithResource_{simplifiedTypeName}<TMcpServerResourceType>(
                this {G.IMcpServerResourcesBuilder} builder,
                {G.Func}<TMcpServerResourceType> resourceFactory,
                {G.CreationMode} creationMode = {G.CreationMode}.Singleton)
                where TMcpServerResourceType : class
            """;

        return builder.AddMethodDeclaration(signature, m => m
            .WithSummaryComment($"拦截 WithResource&lt;{resourceType.Name}&gt; 方法调用。")
            .AddAttributes(models.Select(GenerateInterceptsLocationAttribute))
            .AddRawStatement($$"""
                {{G.Func}}<{{resourceType.ToUsingString()}}> typedFactory = creationMode switch
                {
                    {{G.CreationMode}}.Singleton => () => builder.Resources.GetOrAddSingleton<{{resourceType.ToUsingString()}}>("{{resourceType.ToDisplayString()}}", _ => ({{resourceType.ToUsingString()}})(object)resourceFactory()),
                    _ => () => ({{resourceType.ToUsingString()}})(object)resourceFactory(),
                };
                """)
            .AddRawStatements(firstModel.ResourceModels.SelectMany(resourceModel => GenerateResourceBridgeCreation(resourceModel, resourceType)))
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
    private static string GenerateInterceptsLocationAttribute(WithResourceInterceptorGeneratingModel model)
    {
        var location = model.InterceptableLocation;
        var displayLocation = location.GetDisplayLocation();
        return $"""[global::System.Runtime.CompilerServices.InterceptsLocation({location.Version}, "{location.Data}")] // {displayLocation}""";
    }

    /// <summary>
    /// 生成资源桥接实例创建代码。
    /// </summary>
    private static IEnumerable<string> GenerateResourceBridgeCreation(McpServerResourceGeneratingModel resourceModel, INamedTypeSymbol resourceType)
    {
        var bridgeTypeName = resourceModel.GetBridgeTypeName();
        yield return $$"""
            // 为 {{resourceModel.Method.Name}} 方法创建桥接资源
            var resource_{{resourceModel.Method.Name}} = new global::{{resourceModel.Namespace}}.{{bridgeTypeName}}(typedFactory);
            builder.WithResource<TMcpServerResourceType>(resource_{{resourceModel.Method.Name}});
            """;
    }
}
