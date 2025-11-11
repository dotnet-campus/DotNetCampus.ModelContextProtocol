using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

public record McpServerToolGeneratingModel
{
    public required string? Namespace { get; init; }

    public required INamedTypeSymbol ContainingType { get; init; }

    public required IMethodSymbol Method { get; init; }

    public required string Name { get; init; }

    public required string? Title { get; init; }

    public required string? Description { get; init; }

    private static readonly SymbolDisplayFormat SimpleContainingTypeFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    public static McpServerToolGeneratingModel TryParse(IMethodSymbol methodSymbol, CancellationToken cancellationToken)
    {
        // 解析所有 McpServerToolAttribute 特性中的参数
        var attribute = methodSymbol.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == typeof(McpServerToolAttribute).FullName);
        if (attribute == null)
        {
            throw new InvalidOperationException("Method does not have McpServerToolAttribute");
        }

        return new McpServerToolGeneratingModel
        {
            Namespace = methodSymbol.ContainingType.ContainingNamespace.ToDisplayString(),
            ContainingType = methodSymbol.ContainingType,
            Method = methodSymbol,
            Name = attribute.NamedArguments.GetValueOrDefault<string>(nameof(McpServerToolAttribute.Name))
                   ?? NamingHelper.MakeSnakeCase(methodSymbol.Name, true, true),
            Title = attribute.NamedArguments.GetValueOrDefault<string>(nameof(McpServerToolAttribute.Title)),
            Description = "描述信息未提供",
        };
    }

    public string GetGetAccessModifier()
    {
        var accessibility = (Accessibility)Math.Min((int)ContainingType.DeclaredAccessibility, (int)Method.DeclaredAccessibility);
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => throw new NotSupportedException($"Unsupported accessibility: {Method.DeclaredAccessibility}"),
        };
    }

    public string GetBridgeTypeName()
    {
        var name = ContainingType.ToDisplayString(SimpleContainingTypeFormat).Replace('.', '_');
        return $"{name}_{Method.Name}_Bridge";
    }
}
