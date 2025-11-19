using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using DotNetCampus.ModelContextProtocol.Generators.SourceBuilders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators;

/// <summary>
/// MCP 资源源生成器。
/// </summary>
[Generator(LanguageNames.CSharp)]
public class McpServerResourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var resourceMethodProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                typeof(McpServerResourceAttribute).FullName!,
                (n, ct) => n is MethodDeclarationSyntax,
                (c, ct) =>
                {
                    if (c.SemanticModel.GetDeclaredSymbol(c.TargetNode, ct) is not IMethodSymbol methodSymbol)
                    {
                        return null;
                    }

                    return McpServerResourceGeneratingModel.TryParse(methodSymbol, ct);
                })
            .Where(m => m is not null)
            .Select((m, ct) => m!);

        context.RegisterSourceOutput(resourceMethodProvider, Execute);
    }

    private void Execute(SourceProductionContext context, McpServerResourceGeneratingModel model)
    {
        using var builder = new SourceTextBuilder(model.Namespace)
            .Using("System")
            .Using("DotNetCampus.ModelContextProtocol.CompilerServices")
            .Using("DotNetCampus.ModelContextProtocol.Protocol.Messages")
            .Using("DotNetCampus.ModelContextProtocol.Exceptions")
            .AddTypeDeclaration(
                $"{model.GetAccessModifier()} sealed class {model.GetBridgeTypeName()}({G.Func}<{model.ContainingType.ToUsingString()}> targetFactory)",
                t => t.AddBaseTypes(G.IMcpServerResource)
                    .WithSummaryComment($"为 <see cref=\"{model.ContainingType.ToUsingString()}.{model.Method.Name}\"/> 方法生成的 MCP 服务器资源桥接类。")
                    .AddRawText($"private {model.ContainingType.ToUsingString()} Target => targetFactory();")
                    .AddRawText($"/// <inheritdoc />\npublic string ResourceName => \"{model.Name}\";")
                    .AddRawText($"/// <inheritdoc />\npublic string UriTemplate => \"{model.UriTemplate}\";")
                    .AddRawText($"/// <inheritdoc />\npublic bool IsTemplate => {model.IsTemplate.ToString().ToLowerInvariant()};")
                    .AddRawText($"/// <inheritdoc />\npublic string? MimeType => {(model.MimeType is { } mimeType ? $"\"{mimeType}\"" : "null")};")
                    .AddGetResourceDefinitionMethod(model)
                    .AddReadResourceMethod(model)
            );

        var code = builder.ToString();
        context.AddSource($"{model.Namespace}/{model.ContainingType.ToDeclarationNestedDisplayString()}.{model.Method.Name}.cs", code);
    }
}
