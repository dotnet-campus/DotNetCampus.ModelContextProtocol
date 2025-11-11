using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;
using System.Xml.Linq;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

public record McpServerToolGeneratingModel
{
    public required string? Namespace { get; init; }

    public required INamedTypeSymbol ContainingType { get; init; }

    public required IMethodSymbol Method { get; init; }

    public required string Name { get; init; }

    public required string? Title { get; init; }

    public required string? Description { get; init; }

    public required IReadOnlyList<ToolParameterModel> Parameters { get; init; }

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
            Description = ExtractMethodSummary(methodSymbol),
            Parameters = methodSymbol.Parameters
                .Where(p => !IsCancellationTokenParameter(p))
                .Select(p => ToolParameterModel.Parse(p, methodSymbol))
                .ToList(),
        };
    }

    /// <summary>
    /// 从方法的 XML 文档注释中提取 summary 描述。
    /// </summary>
    private static string? ExtractMethodSummary(IMethodSymbol methodSymbol)
    {
        var xmlComment = methodSymbol.GetDocumentationCommentXml(cancellationToken: CancellationToken.None);
        if (string.IsNullOrWhiteSpace(xmlComment))
        {
            return null;
        }

        try
        {
            var doc = XDocument.Parse(xmlComment);
            var summaryElement = doc.Descendants("summary").FirstOrDefault();
            if (summaryElement == null)
            {
                return null;
            }

            return summaryElement.Value.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 判断参数是否为 CancellationToken 类型。
    /// </summary>
    private static bool IsCancellationTokenParameter(IParameterSymbol parameter)
    {
        return parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken";
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

    public string GetBridgeTypeName()
    {
        var name = ContainingType.ToDisplayString(SimpleContainingTypeFormat).Replace('.', '_');
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
