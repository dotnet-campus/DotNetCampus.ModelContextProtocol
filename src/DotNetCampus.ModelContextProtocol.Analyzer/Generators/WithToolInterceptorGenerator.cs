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
public class WithToolInterceptorGenerator : IIncrementalGenerator
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
        using var builder = new SourceTextBuilder
            {
                RemoveIndentForPreprocessorLines = true,
            }
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
    /// 添加拦截器方法（为同一工具类型的所有拦截位置生成方法）。
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

        foreach (var kindModels in models.GroupBy(x => x.InvocationKind))
        {
            builder = builder.AddInterceptorMethodByKind(kindModels.ToList(), toolType);
        }

        return builder;
    }

    private static IAllowMemberDeclaration AddInterceptorMethodByKind(
        this IAllowMemberDeclaration builder,
        List<WithToolInterceptorGeneratingModel> models,
        INamedTypeSymbol toolType)
    {
        var simplifiedTypeName = NamingHelper.MakePascalCase(toolType.ToDisplayString());
        var invocationKind = models[0].InvocationKind;

        var signature = invocationKind switch
        {
            WithToolInvocationKind.WithFactory => $"""
                public static {G.IMcpServerToolsBuilder} WithTool_{simplifiedTypeName}<TMcpServerToolType>(
                    this {G.IMcpServerToolsBuilder} builder,
                    {G.Func}<TMcpServerToolType> toolFactory,
                    {G.CreationMode} creationMode = {G.CreationMode}.Singleton)
                    where TMcpServerToolType : class
                """,
            WithToolInvocationKind.WithoutFactory => $"""
                public static {G.IMcpServerToolsBuilder} WithTool_{simplifiedTypeName}<TMcpServerToolType>(
                    this {G.IMcpServerToolsBuilder} builder,
                    {G.CreationMode} creationMode = {G.CreationMode}.Singleton)
                    where TMcpServerToolType : class
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(invocationKind), invocationKind, null),
        };

        return builder.AddMethodDeclaration(signature, m => m
            .WithSummaryComment($"拦截 WithTool&lt;{toolType.Name}&gt; 方法调用。")
            .AddAttributes(models.Select(GenerateInterceptsLocationAttribute))
            .AddRawStatement(GenerateTypedFactoryStatement(toolType, invocationKind))
            .AddRawStatements(models[0].ToolModels.SelectMany(toolModel => GenerateToolBridgeCreation(toolModel, toolType)))
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

    private static string GenerateTypedFactoryStatement(INamedTypeSymbol toolType, WithToolInvocationKind invocationKind)
    {
        return invocationKind switch
        {
            WithToolInvocationKind.WithFactory => $$"""
                {{G.Func}}<{{toolType.ToUsingString()}}> typedFactory = creationMode switch
                {
                    {{G.CreationMode}}.Singleton => () => builder.Tools.GetOrAddSingleton<{{toolType.ToUsingString()}}>("{{toolType.ToDisplayString()}}", _ => ({{toolType.ToUsingString()}})(object)toolFactory()),
                    _ => () => ({{toolType.ToUsingString()}})(object)toolFactory(),
                };
                """,
            WithToolInvocationKind.WithoutFactory => GenerateTypedFactoryForNoFactoryInvocation(toolType),
            _ => throw new ArgumentOutOfRangeException(nameof(invocationKind), invocationKind, null),
        };
    }

    private static string GenerateTypedFactoryForNoFactoryInvocation(INamedTypeSymbol toolType)
    {
        var constructor = SelectConstructor(toolType);
        if (constructor is null)
        {
            return $$"""
                #error 无法拦截 WithTool<{{toolType.ToDisplayString()}}>()：未找到可访问的实例构造函数。请为该类型提供 public/internal/protected internal 构造函数，或改用 WithTool(() => new {{toolType.ToDisplayString()}}(...))。
                {{G.Func}}<{{toolType.ToUsingString()}}> typedFactory = () => throw new global::System.InvalidOperationException("无法创建 {{toolType.ToDisplayString()}} 实例，因为未找到可访问的实例构造函数。");
                """;
        }

        if (constructor.Parameters.Length == 0)
        {
            var creationExpression = GenerateToolCreationExpression(toolType, constructor, "_");
            return $$"""
                {{G.Func}}<{{toolType.ToUsingString()}}> typedFactory = creationMode switch
                {
                    {{G.CreationMode}}.Singleton => () => builder.Tools.GetOrAddSingleton<{{toolType.ToUsingString()}}>("{{toolType.ToDisplayString()}}", _ => {{creationExpression}}),
                    _ => () => {{creationExpression}},
                };
                """;
        }

        var toolCreationExpression = GenerateToolCreationExpression(toolType, constructor, "serviceProvider");
        return $$"""
            static {{toolType.ToUsingString()}} CreateTool(global::DotNetCampus.ModelContextProtocol.Servers.McpServer server)
            {
                var serviceProvider = server.Context.ServiceProvider
                    ?? throw new global::System.InvalidOperationException("无委托 WithTool<T>() 需要可用的 IServiceProvider，请先调用 McpServerBuilder.WithServices。\n当前工具类型：{{toolType.ToDisplayString()}}。");
                return {{toolCreationExpression}};
            }

            var server = builder.Tools.GetOrAddSingleton<global::DotNetCampus.ModelContextProtocol.Servers.McpServer>("DotNetCampus.ModelContextProtocol.Server.{{toolType.ToDisplayString()}}", s => s);
            {{G.Func}}<{{toolType.ToUsingString()}}> typedFactory = creationMode switch
            {
                {{G.CreationMode}}.Singleton => () => builder.Tools.GetOrAddSingleton<{{toolType.ToUsingString()}}>("{{toolType.ToDisplayString()}}", CreateTool),
                _ => () => CreateTool(server),
            };
            """;
    }

    private static string GenerateToolCreationExpression(INamedTypeSymbol toolType, IMethodSymbol constructor, string serviceProviderVariableName)
    {
        if (constructor.Parameters.Length == 0)
        {
            return $$"""new {{toolType.ToUsingString()}}()""";
        }

        var arguments = constructor.Parameters.Select(p =>
            $$"""(({{p.Type.ToUsingString()}}?){{serviceProviderVariableName}}.GetService(typeof({{p.Type.ToUsingString()}})) ?? throw new global::System.InvalidOperationException("无委托 WithTool<T>() 无法创建 {{toolType.ToDisplayString()}}：未找到构造函数参数服务 '{{p.Type.ToDisplayString()}}'。请确保已通过 McpServerBuilder.WithServices 提供该服务。"))""");
        return $$"""new {{toolType.ToUsingString()}}({{string.Join(", ", arguments)}})""";
    }

    private static IMethodSymbol? SelectConstructor(INamedTypeSymbol toolType)
    {
        var constructors = toolType.InstanceConstructors
            .Where(IsAccessibleFromGeneratedCode)
            .ToList();

        if (constructors.Count == 0)
        {
            return null;
        }

        return constructors
            .OrderByDescending(c => c.Parameters.Length)
            .ThenBy(c => c.IsImplicitlyDeclared)
            .First();
    }

    private static bool IsAccessibleFromGeneratedCode(IMethodSymbol constructor)
    {
        return constructor.DeclaredAccessibility is Accessibility.Public
            or Accessibility.Internal
            or Accessibility.ProtectedOrInternal;
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
