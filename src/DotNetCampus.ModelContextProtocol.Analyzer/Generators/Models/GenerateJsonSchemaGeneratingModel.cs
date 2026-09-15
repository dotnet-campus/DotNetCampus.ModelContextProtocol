using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

internal sealed record GenerateJsonSchemaGeneratingModel
{
    public required string Namespace { get; init; }

    public required string JsonSerializerContextFullName { get; init; }

    public required string JsonSerializerContextName { get; init; }

    public required bool IsJsonSerializerContextPublic { get; init; }

    public required ITypeSymbol Type { get; init; }

    public string GetAccessModifier() => IsJsonSerializerContextPublic && Type.IsEffectivelyPublic()
        ? "public"
        : "internal";

    public string GetExtensionTypeName()
    {
        var name = $"{JsonSerializerContextName}_{Type.ToDeclarationNestedDisplayString()}_JsonSchemaExtensions";
        return SanitizeIdentifier(name);
    }

    private static string SanitizeIdentifier(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars);
    }
}

internal static class GenerateJsonSchemaTypeSymbolExtensions
{
    extension(ITypeSymbol type)
    {
        public bool IsEffectivelyPublic()
        {
            var notNull = type.GetNotNullTypeSymbol();
            return notNull switch
            {
                IArrayTypeSymbol arrayType => arrayType.ElementType.IsEffectivelyPublic(),
                INamedTypeSymbol namedType => IsNamedTypeEffectivelyPublic(namedType),
                { DeclaredAccessibility: Accessibility.Public } => true,
                _ => false,
            };
        }
    }

    private static bool IsNamedTypeEffectivelyPublic(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not Accessibility.Public)
            {
                return false;
            }
        }

        foreach (var typeArgument in type.TypeArguments)
        {
            if (!typeArgument.IsEffectivelyPublic())
            {
                return false;
            }
        }

        return true;
    }
}
