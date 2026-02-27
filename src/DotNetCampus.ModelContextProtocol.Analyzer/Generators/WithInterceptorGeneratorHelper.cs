using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators;

internal static class WithInterceptorGeneratorHelper
{
    public static IMethodSymbol? SelectConstructor(INamedTypeSymbol targetType)
    {
        var constructors = targetType.InstanceConstructors
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

    public static string GenerateCreationExpression(
        INamedTypeSymbol targetType,
        IMethodSymbol constructor,
        string serviceProviderVariableName,
        string apiName)
    {
        var targetTypeName = ToTypeReference(targetType);

        if (constructor.Parameters.Length == 0)
        {
            return $"new {targetTypeName}()";
        }

        var arguments = constructor.Parameters.Select(p =>
            $"""        (({ToTypeReference(p.Type)}?){serviceProviderVariableName}.GetService(typeof({ToTypeReference(p.Type)})) ?? throw new global::System.InvalidOperationException("无委托 {apiName}<T>() 无法创建 {targetType.ToDisplayString()}：未找到构造函数参数服务 '{p.Type.ToDisplayString()}'。请确保已通过 McpServerBuilder.WithServices 提供该服务。"))""");
        return $"""
            new {targetTypeName}(
            {string.Join(",\n", arguments)}
                )
            """;
    }

    private static string ToTypeReference(ITypeSymbol typeSymbol)
    {
        return typeSymbol.WithNullableAnnotation(NullableAnnotation.None)
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static bool IsAccessibleFromGeneratedCode(IMethodSymbol constructor)
    {
        return constructor.DeclaredAccessibility is Accessibility.Public
            or Accessibility.Internal
            or Accessibility.ProtectedOrInternal;
    }
}
