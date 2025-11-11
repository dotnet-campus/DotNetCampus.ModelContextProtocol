using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        var code = GenerateMcpServerToolBridgeCode(model);
        context.AddSource($"ModelContextProtocol.Bridges/{model.ContainingType.ToDisplayString()}.{model.Method.Name}.cs", code);
    }

    private string GenerateMcpServerToolBridgeCode(McpServerToolGeneratingModel model)
    {
        var targetFactory = $"global::System.Func<{model.ContainingType.ToUsingString()}>";
        var builder = new SourceTextBuilder(model.Namespace)
            .AddTypeDeclaration($"{model.GetGetAccessModifier()} sealed class {model.GetBridgeTypeName()}({targetFactory} targetFactory)", t => t
                .AddBaseTypes("global::DotNetCampus.ModelContextProtocol.CompilerServices.IGeneratedMcpServerToolBridge")
                .WithSummaryComment($"为 <see cref=\"{model.ContainingType.ToUsingString()}.{model.Method.Name}\"/> 方法生成的 MCP 服务器工具桥接类。")
                .AddRawText($"private readonly {targetFactory} _targetFactory = targetFactory;")
                .AddRawText($"private {model.ContainingType.ToUsingString()} Target => _targetFactory();")
                .AddRawText($"/// <inheritdoc />\npublic string ToolName {{ get; }} = \"{model.Name}\";")
                .AddGetToolDefinitionMethod(model)
                .AddCallToolMethod(model)
            );
        return builder.ToString();
    }
}

file static class Extensions
{
    public static IAllowMemberDeclaration AddGetToolDefinitionMethod(this IAllowMemberDeclaration builder, McpServerToolGeneratingModel model)
    {
        return builder.AddMethodDeclaration("public global::DotNetCampus.ModelContextProtocol.Protocol.Tool GetToolDefinition()", m => m
            .WithRawDocumentationComment("/// <inheritdoc />")
            .AddBracketScope("return new()", "{", "};", b => b
                .AddPropertyAssignment("Name", model.Name)
                .AddPropertyAssignment("Title", model.Title)
                .AddPropertyAssignment("Description", model.Description)
            )
        );
    }

    public static IAllowMemberDeclaration AddCallToolMethod(this IAllowMemberDeclaration builder, McpServerToolGeneratingModel model)
    {
        const string valueTask = "global::System.Threading.Tasks.ValueTask";
        const string callToolResult = "global::DotNetCampus.ModelContextProtocol.Protocol.CallToolResult";
        const string jsonElement = "global::System.Text.Json.JsonElement";
        const string jsonSerializerContext = "global::System.Text.Json.Serialization.JsonSerializerContext";
        const string signature = $"public {valueTask}<{callToolResult}> CallTool({jsonElement} jsonArguments, {jsonSerializerContext} jsonSerializerContext)";
        return builder.AddMethodDeclaration(signature, m => m
            .WithRawDocumentationComment("/// <inheritdoc />")
            .AddRawText("// 方法体省略，具体实现根据方法参数和返回值生成。")
            .AddRawText("throw new global::System.NotImplementedException();")
        );
    }

    private static ISourceTextBuilder AddPropertyAssignment(this ISourceTextBuilder builder, string propertyName, string? stringValue)
    {
        builder.AddRawText($"{propertyName} = {stringValue switch {
            null => "null",
            _ => $"\"{stringValue}\"",
        }},");
        return builder;
    }
}
