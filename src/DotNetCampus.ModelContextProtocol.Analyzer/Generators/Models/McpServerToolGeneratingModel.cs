using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

public record McpServerToolGeneratingModel
{
    public required string? Namespace { get; init; }

    public required INamedTypeSymbol ContainingType { get; init; }

    public required IMethodSymbol Method { get; init; }

    public required string Name { get; init; }

    public required string? Title { get; init; }

    public required string? Description { get; init; }

    public IReadOnlyList<IParameterSymbol> GetParameters(bool includeAll = false)
    {
        return Method.Parameters
            .Where(p => includeAll || !p.IsCancellationTokenParameter())
            .ToList();
    }

    public IReadOnlyList<JsonPropertySchemaInfo> GetProperties()
    {
        return Method.Parameters
            .Where(p => !p.IsCancellationTokenParameter())
            .Select(JsonPropertySchemaInfo.From)
            .ToList();
    }

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
            Description = methodSymbol.GetSummaryFromSymbol(),
        };
    }

    public bool GetIsAsync() => IsTaskLikeReturnType(Method.ReturnType);

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

    /// <summary>
    /// 获取桥接类的名称。
    /// </summary>
    /// <returns>桥接类的名称。</returns>
    public string GetBridgeTypeName()
    {
        var name = ContainingType.ToDeclarationNestedDisplayString().Replace('.', '_');
        return $"{name}_{Method.Name}_Bridge";
    }

    /// <summary>
    /// 判断返回类型是否为 Task 或 ValueTask。
    /// </summary>
    private static bool IsTaskLikeReturnType(ITypeSymbol returnType)
    {
        if (returnType is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var fullName = namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return fullName is "global::System.Threading.Tasks.Task<TResult>"
            or "global::System.Threading.Tasks.ValueTask<TResult>"
            or "global::System.Threading.Tasks.Task"
            or "global::System.Threading.Tasks.ValueTask";
    }
}
