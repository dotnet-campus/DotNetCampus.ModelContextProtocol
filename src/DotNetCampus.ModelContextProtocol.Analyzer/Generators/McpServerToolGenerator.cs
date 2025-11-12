using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetCampus.ModelContextProtocol.Generators.SourceBuilders;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators;

[Generator(LanguageNames.CSharp)]
public class McpServerToolGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var toolMethodProvider = context.SyntaxProvider.ForAttributeWithMetadataName(typeof(McpServerToolAttribute).FullName!,
                (n, ct) => n is MethodDeclarationSyntax, (c, ct) =>
                {
                    if (c.SemanticModel.GetDeclaredSymbol(c.TargetNode, ct) is not IMethodSymbol methodSymbol)
                    {
                        return null;
                    }

                    return McpServerToolGeneratingModel.TryParse(methodSymbol, ct);
                })
            .Where(m => m is not null)
            .Select((m, ct) => m!);

        context.RegisterSourceOutput(toolMethodProvider, Execute);
    }

    private void Execute(SourceProductionContext context, McpServerToolGeneratingModel model)
    {
        try
        {
            using var builder = new SourceTextBuilder(model.Namespace)
                .Using("System.Text.Json")
                .AddTypeDeclaration(
                    $"{model.GetGetAccessModifier()} sealed class {model.GetBridgeTypeName()}({G.Func}<{model.ContainingType.ToUsingString()}> targetFactory)",
                    t => t.AddBaseTypes(G.IMcpServerTool)
                        .WithSummaryComment($"为 <see cref=\"{model.ContainingType.ToUsingString()}.{model.Method.Name}\"/> 方法生成的 MCP 服务器工具桥接类。")
                        .AddRawText($"private readonly {G.Func}<{model.ContainingType.ToUsingString()}> _targetFactory = targetFactory;")
                        .AddRawText($"private {model.ContainingType.ToUsingString()} Target => _targetFactory();")
                        .AddRawText($"/// <inheritdoc />\npublic string ToolName {{ get; }} = \"{model.Name}\";")
                        .AddGetToolDefinitionMethod(model)
                        .AddGetInputSchemaMethod(model)
                        .AddCallToolMethod(model)
                );
            var code = builder.ToString();
            context.AddSource($"{model.Namespace}/{model.ContainingType.ToTypeOnlyString()}.{model.Method.Name}.cs", code);
        }
        catch (Exception ex)
        {
            throw new NotImplementedException(ex.ToString().Replace("\n", " ").Replace("\r", ""));
        }
    }
}
