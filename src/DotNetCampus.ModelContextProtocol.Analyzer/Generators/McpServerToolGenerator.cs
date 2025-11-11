using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetCampus.ModelContextProtocol.Generators;

[Generator(LanguageNames.CSharp)]
public class McpServerToolGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.SyntaxProvider.ForAttributeWithMetadataName(typeof(McpServerToolAttribute).FullName!, (n, ct) => n is MethodDeclarationSyntax, (c, ct) =>
        {
            if (c.SemanticModel.GetDeclaredSymbol(c.TargetNode, ct) is not IMethodSymbol methodSymbol)
            {
                return null;
            }

            return McpServerToolGeneratingModel.TryParse(methodSymbol, ct);
        });
    }
}

public record McpServerToolGeneratingModel
{
    public static McpServerToolGeneratingModel TryParse(IMethodSymbol methodSymbol, CancellationToken cancellationToken)
    {
        // 解析所有 McpServerToolAttribute 特性中的参数
        var attribute = methodSymbol.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == typeof(McpServerToolAttribute).FullName);
        if (attribute == null)
        {
            throw new InvalidOperationException("Method does not have McpServerToolAttribute");
        }

        // var name = attribute.NamedArguments.TryGetValue<string>(nameof(McpServerToolAttribute.Name));

        return new McpServerToolGeneratingModel();
    }
}

internal static class AttributeDataExtensions
{
    public static bool TryGetValue<T>(this ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments, string name, [NotNullWhen(true)] out T? value)
    {
        var argument = namedArguments.FirstOrDefault(x => x.Key == name);
        if (argument.Value.IsNull)
        {
            value = default;
            return false;
        }

        value = typeof(T) switch
        {
            var t when t == typeof(string) => (T)(object)argument.Value.Value!.ToString(),
            var t when t == typeof(bool) => (T)(object)(bool)argument.Value.Value!,
            { IsEnum: true } => (T)argument.Value.Value!,
            _ => throw new NotSupportedException($"Type {typeof(T)} is not supported"),
        };
        return true;
    }
}
