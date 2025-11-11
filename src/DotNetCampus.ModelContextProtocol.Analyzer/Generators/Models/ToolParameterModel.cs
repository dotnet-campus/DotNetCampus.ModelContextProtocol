using System.Xml.Linq;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// 工具参数模型。
/// </summary>
public record ToolParameterModel
{
    /// <summary>
    /// 参数名称（原始名称）。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 参数在 JSON 中的名称。
    /// </summary>
    public required string JsonName { get; init; }

    /// <summary>
    /// 参数类型符号。
    /// </summary>
    public required ITypeSymbol Type { get; init; }

    /// <summary>
    /// 参数类型的完整名称。
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// 参数是否必需。
    /// </summary>
    public required bool IsRequired { get; init; }

    /// <summary>
    /// 参数描述。
    /// </summary>
    public string? Description { get; init; }

    public string? GetJsonEscapedDescription()
    {
        return Description?
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "")
            .Replace("\n", " ");
    }

    public static ToolParameterModel Parse(IParameterSymbol parameter, IMethodSymbol method)
    {
        return new ToolParameterModel
        {
            Name = parameter.Name,
            JsonName = NamingHelper.MakeKebabCase(parameter.Name, true, true),
            Type = parameter.Type,
            TypeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            IsRequired = !parameter.HasExplicitDefaultValue,
            Description = ExtractParameterDescription(parameter, method),
        };
    }

    private static string? ExtractParameterDescription(IParameterSymbol parameter, IMethodSymbol method)
    {
        var xmlComment = method.GetDocumentationCommentXml(cancellationToken: CancellationToken.None);
        if (string.IsNullOrWhiteSpace(xmlComment))
        {
            return null;
        }

        try
        {
            var doc = XDocument.Parse(xmlComment);
            var paramElement = doc.Descendants("param")
                .FirstOrDefault(e => e.Attribute("name")?.Value == parameter.Name);

            if (paramElement == null)
            {
                return null;
            }

            return paramElement.Value.Trim();
        }
        catch
        {
            return null;
        }
    }
}
