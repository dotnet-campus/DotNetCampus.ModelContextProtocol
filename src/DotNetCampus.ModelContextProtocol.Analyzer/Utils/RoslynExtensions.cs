using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetCampus.ModelContextProtocol.Utils;

/// <summary>
/// Roslyn 扩展方法，用于处理文档注释等功能。
/// </summary>
internal static class RoslynExtensions
{
    /// <summary>
    /// 获取语法节点的文档注释 Trivia。
    /// </summary>
    /// <remarks>
    /// 此方法解决了源生成器在用户未启用 GenerateDocumentationFile 时无法获取 XML 文档注释的问题。
    /// 参考：https://github.com/Cysharp/ConsoleAppFramework/blob/1b9df49d29b3b4ed60c538a54b3070e86d35a9b5/src/ConsoleAppFramework/RoslynExtensions.cs#L92
    ///
    /// 原理：
    /// - ISymbol.GetDocumentationCommentXml() 需要 &lt;GenerateDocumentationFile&gt;true&lt;/&gt;
    /// - 获取 DocumentationCommentTrivia 也需要相同条件
    /// - 当 DocumentationMode 为 None 时，手动重新解析代码并强制启用 DocumentationMode.Parse
    /// </remarks>
    public static DocumentationCommentTriviaSyntax? GetDocumentationCommentTriviaSyntax(this SyntaxNode node)
    {
        // 如果 DocumentationMode 为 None（即用户未启用文档生成），则手动重新解析
        if (node.SyntaxTree.Options.DocumentationMode == DocumentationMode.None)
        {
            var withDocumentationComment = node.SyntaxTree.Options.WithDocumentationMode(DocumentationMode.Parse);
            var code = node.ToFullString();
            var newTree = CSharpSyntaxTree.ParseText(code, (CSharpParseOptions)withDocumentationComment);
            node = newTree.GetRoot();
        }

        // 在 leading trivia 中查找文档注释
        foreach (var leadingTrivia in node.GetLeadingTrivia())
        {
            if (leadingTrivia.GetStructure() is DocumentationCommentTriviaSyntax structure)
            {
                return structure;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取指定名称的 XML 元素。
    /// </summary>
    private static IEnumerable<XmlNodeSyntax> GetXmlElements(this SyntaxList<XmlNodeSyntax> content, string elementName)
    {
        foreach (XmlNodeSyntax syntax in content)
        {
            if (syntax is XmlEmptyElementSyntax emptyElement)
            {
                if (string.Equals(elementName, emptyElement.Name.ToString(), StringComparison.Ordinal))
                {
                    yield return emptyElement;
                }
                continue;
            }

            if (syntax is XmlElementSyntax elementSyntax)
            {
                if (string.Equals(elementName, elementSyntax.StartTag?.Name?.ToString(), StringComparison.Ordinal))
                {
                    yield return elementSyntax;
                }
                continue;
            }
        }
    }

    /// <summary>
    /// 从文档注释中提取 summary 内容。
    /// </summary>
    public static string? GetSummary(this DocumentationCommentTriviaSyntax docComment)
    {
        var summary = docComment.Content.GetXmlElements("summary").FirstOrDefault() as XmlElementSyntax;
        if (summary == null) return null;

        return ParseTextFromXmlNodeSyntaxList(summary.Content);
    }

    /// <summary>
    /// 从文档注释中提取所有 param 参数的描述。
    /// </summary>
    public static IEnumerable<(string Name, string Description)> GetParams(this DocumentationCommentTriviaSyntax docComment)
    {
        foreach (var item in docComment.Content.GetXmlElements("param").OfType<XmlElementSyntax>())
        {
            var name = item.StartTag.Attributes
                .OfType<XmlNameAttributeSyntax>()
                .FirstOrDefault()?
                .Identifier.Identifier.ValueText ?? "";

            var desc = ParseTextFromXmlNodeSyntaxList(item.Content);

            yield return (name, desc);
        }
    }

    /// <summary>
    /// 从 XmlNodeSyntax 列表中解析文本内容。
    /// </summary>
    /// <param name="content">注释文本。</param>
    /// <returns>解析后的纯文本。</returns>
    private static string ParseTextFromXmlNodeSyntaxList(SyntaxList<XmlNodeSyntax> content)
    {
        var regex = new Regex(@"\s+///\s?(.*)");
        var text = content.ToString();

        // 按行拆分
        IReadOnlyList<string> lines = text.Split(['\n'], StringSplitOptions.None);
        if (lines.Count is 0 or 1)
        {
            return text.Trim();
        }

        // 去除每行开头的空格和 ///，去除行尾的 \r
        var trimmedLines = lines.Select(line =>
        {
            line = line.TrimEnd('\r');
            var match = regex.Match(line);
            if (match is { Success: true, Groups.Count: > 1 })
            {
                return match.Groups[1].Value;
            }
            return line;
        }).ToList();

        // 删除首行和尾行的空行（如果是）
        if (trimmedLines[^1].Length is 0)
        {
            trimmedLines.RemoveAt(trimmedLines.Count - 1);
        }
        if (trimmedLines[0].Length is 0)
        {
            trimmedLines.RemoveAt(0);
        }
        return string.Join("\n", trimmedLines);
    }

    /// <summary>
    /// 从方法符号获取 summary 描述。
    /// </summary>
    public static string? GetSummaryFromSymbol(this IMethodSymbol methodSymbol)
    {
        // 尝试从语法节点获取文档注释
        var syntaxRef = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef?.GetSyntax() is MethodDeclarationSyntax methodSyntax)
        {
            var docComment = methodSyntax.GetDocumentationCommentTriviaSyntax();
            if (docComment != null)
            {
                return docComment.GetSummary();
            }
        }

        return null;
    }

    /// <summary>
    /// 从方法符号获取参数描述字典。
    /// </summary>
    public static Dictionary<string, string> GetParameterDescriptions(this IMethodSymbol methodSymbol)
    {
        var result = new Dictionary<string, string>();

        var syntaxRef = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef?.GetSyntax() is MethodDeclarationSyntax methodSyntax)
        {
            var docComment = methodSyntax.GetDocumentationCommentTriviaSyntax();
            if (docComment != null)
            {
                foreach (var (name, description) in docComment.GetParams())
                {
                    result[name] = description;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 从属性符号获取 summary 描述。
    /// </summary>
    public static string? GetSummaryFromSymbol(this IPropertySymbol propertySymbol)
    {
        var syntaxRef = propertySymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef?.GetSyntax() is PropertyDeclarationSyntax propertySyntax)
        {
            var docComment = propertySyntax.GetDocumentationCommentTriviaSyntax();
            if (docComment != null)
            {
                return docComment.GetSummary();
            }
        }

        return null;
    }

    /// <summary>
    /// 从字段符号获取 summary 描述。
    /// </summary>
    public static string? GetSummaryFromSymbol(this IFieldSymbol fieldSymbol)
    {
        var syntaxRef = fieldSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef?.GetSyntax() is VariableDeclaratorSyntax variableDeclarator)
        {
            // 字段的文档注释在 FieldDeclarationSyntax 上
            if (variableDeclarator.Parent?.Parent is FieldDeclarationSyntax fieldSyntax)
            {
                var docComment = fieldSyntax.GetDocumentationCommentTriviaSyntax();
                if (docComment != null)
                {
                    return docComment.GetSummary();
                }
            }
        }
        else if (syntaxRef?.GetSyntax() is EnumMemberDeclarationSyntax enumMemberSyntax)
        {
            var docComment = enumMemberSyntax.GetDocumentationCommentTriviaSyntax();
            if (docComment != null)
            {
                return docComment.GetSummary();
            }
        }

        return null;
    }

    /// <summary>
    /// 从参数符号获取 param 描述。
    /// </summary>
    public static string? GetParameterDescription(this IParameterSymbol parameterSymbol)
    {
        // 参数的文档注释在其所属方法或构造函数上
        var containingSymbol = parameterSymbol.ContainingSymbol;
        if (containingSymbol is IMethodSymbol methodSymbol)
        {
            var descriptions = methodSymbol.GetParameterDescriptions();
            if (descriptions.TryGetValue(parameterSymbol.Name, out var description))
            {
                return description;
            }
        }

        return null;
    }

    /// <summary>
    /// 从类型符号获取 summary 描述。
    /// </summary>
    public static string? GetSummaryFromSymbol(this ITypeSymbol typeSymbol)
    {
        var syntaxRef = typeSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
        {
            return null;
        }

        var syntax = syntaxRef.GetSyntax();
        var docComment = syntax switch
        {
            ClassDeclarationSyntax classSyntax => classSyntax.GetDocumentationCommentTriviaSyntax(),
            InterfaceDeclarationSyntax interfaceSyntax => interfaceSyntax.GetDocumentationCommentTriviaSyntax(),
            StructDeclarationSyntax structSyntax => structSyntax.GetDocumentationCommentTriviaSyntax(),
            RecordDeclarationSyntax recordSyntax => recordSyntax.GetDocumentationCommentTriviaSyntax(),
            EnumDeclarationSyntax enumSyntax => enumSyntax.GetDocumentationCommentTriviaSyntax(),
            _ => null,
        };

        return docComment?.GetSummary();
    }
}
