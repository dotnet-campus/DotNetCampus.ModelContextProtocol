using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// JSON Schema 成员描述符（统一表示参数和属性）。
/// </summary>
/// <param name="JsonName">JSON 属性名（kebab-case）</param>
/// <param name="Type">类型符号</param>
/// <param name="Description">描述文本（已转义）</param>
/// <param name="IsRequired">是否必需</param>
public record SchemaMemberDescriptor(
    string JsonName,
    ITypeSymbol Type,
    string? Description,
    bool IsRequired)
{
    /// <summary>
    /// 从参数创建 Schema 成员描述符。
    /// </summary>
    public static SchemaMemberDescriptor FromParameter(ToolParameterModel parameter)
    {
        return new SchemaMemberDescriptor(
            parameter.JsonName,
            parameter.Type,
            parameter.GetJsonEscapedDescription(),
            parameter.IsRequired
        );
    }

    /// <summary>
    /// 从属性创建 Schema 成员描述符。
    /// </summary>
    public static SchemaMemberDescriptor FromProperty(IPropertySymbol property)
    {
        var jsonName = Utils.NamingHelper.MakeKebabCase(property.Name, true, true);
        var description = GetPropertyDescription(property);
        var isRequired = !IsNullableType(property.Type);

        return new SchemaMemberDescriptor(jsonName, property.Type, description, isRequired);
    }

    /// <summary>
    /// 从构造函数参数创建 Schema 成员描述符。
    /// </summary>
    public static SchemaMemberDescriptor FromConstructorParameter(IParameterSymbol parameter)
    {
        var jsonName = Utils.NamingHelper.MakeKebabCase(parameter.Name, true, true);
        var description = GetParameterDescription(parameter);
        var isRequired = !IsNullableType(parameter.Type) && !parameter.HasExplicitDefaultValue;

        return new SchemaMemberDescriptor(jsonName, parameter.Type, description, isRequired);
    }

    /// <summary>
    /// 获取对象类型的所有属性作为 Schema 成员描述符。
    /// </summary>
    public static SchemaMemberDescriptor[] GetObjectProperties(INamedTypeSymbol typeSymbol)
    {
        var properties = new List<SchemaMemberDescriptor>();

        // 获取所有公共属性
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IPropertySymbol property &&
                property.DeclaredAccessibility == Accessibility.Public &&
                !property.IsStatic)
            {
                properties.Add(FromProperty(property));
            }
        }

        // 处理主构造函数参数（record 类型）
        if (typeSymbol.IsRecord)
        {
            foreach (var ctor in typeSymbol.Constructors)
            {
                if (ctor.DeclaredAccessibility == Accessibility.Public)
                {
                    foreach (var param in ctor.Parameters)
                    {
                        var jsonName = Utils.NamingHelper.MakeKebabCase(param.Name, true, true);
                        // 检查是否已经通过属性添加
                        if (!properties.Any(p => p.JsonName == jsonName))
                        {
                            properties.Add(FromConstructorParameter(param));
                        }
                    }
                    break; // 只处理第一个公共构造函数
                }
            }
        }

        return properties.ToArray();
    }

    /// <summary>
    /// 解包可空类型，返回核心类型和是否可空。
    /// </summary>
    public static (ITypeSymbol CoreType, bool IsNullable) UnwrapNullableType(ITypeSymbol typeSymbol)
    {
        // 处理 Nullable<T> (值类型可空)
        if (typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } namedType)
        {
            return (namedType.TypeArguments[0], true);
        }

        // 处理引用类型可空注解
        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return (typeSymbol.WithNullableAnnotation(NullableAnnotation.NotAnnotated), true);
        }

        return (typeSymbol, false);
    }

    /// <summary>
    /// 获取 C# 类型对应的 JSON Schema 类型。
    /// </summary>
    public static string GetJsonSchemaType(ITypeSymbol typeSymbol)
    {
        // 处理特殊类型
        switch (typeSymbol.SpecialType)
        {
            case SpecialType.System_Boolean:
                return "boolean";
            case SpecialType.System_String:
            case SpecialType.System_Char:
                return "string";
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return "number";
        }

        // 处理数组类型
        if (typeSymbol is IArrayTypeSymbol)
        {
            return "array";
        }

        // 处理集合类型
        if (typeSymbol is INamedTypeSymbol namedType)
        {
            var fullName = namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fullName.StartsWith("global::System.Collections.Generic.IEnumerable<") ||
                fullName.StartsWith("global::System.Collections.Generic.List<") ||
                fullName.StartsWith("global::System.Collections.Generic.IList<") ||
                fullName.StartsWith("global::System.Collections.Generic.IReadOnlyList<") ||
                fullName.StartsWith("global::System.Collections.Generic.ICollection<"))
            {
                return "array";
            }
        }

        // 默认为 object
        return "object";
    }

    /// <summary>
    /// 获取数组或集合的元素类型。
    /// </summary>
    public static ITypeSymbol? GetArrayElementType(ITypeSymbol typeSymbol)
    {
        // 处理数组类型
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType;
        }

        // 处理泛型集合类型
        if (typeSymbol is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
        {
            var fullName = namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fullName.StartsWith("global::System.Collections.Generic.IEnumerable<") ||
                fullName.StartsWith("global::System.Collections.Generic.List<") ||
                fullName.StartsWith("global::System.Collections.Generic.IList<") ||
                fullName.StartsWith("global::System.Collections.Generic.IReadOnlyList<") ||
                fullName.StartsWith("global::System.Collections.Generic.ICollection<"))
            {
                return namedType.TypeArguments[0];
            }
        }

        return null;
    }

    /// <summary>
    /// 判断类型是否可空。
    /// </summary>
    private static bool IsNullableType(ITypeSymbol typeSymbol)
    {
        // 值类型的可空形式
        if (typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            return true;
        }

        // 引用类型的可空注解
        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取属性的描述（从 XML 文档注释）。
    /// </summary>
    private static string? GetPropertyDescription(IPropertySymbol property)
    {
        var xmlComment = property.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xmlComment))
        {
            return null;
        }

        return ExtractSummaryFromXml(xmlComment!);
    }

    /// <summary>
    /// 获取参数的描述（从 XML 文档注释）。
    /// </summary>
    private static string? GetParameterDescription(IParameterSymbol parameter)
    {
        // 从包含方法/构造函数获取 XML 注释
        var containingSymbol = parameter.ContainingSymbol;
        var xmlComment = containingSymbol?.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xmlComment))
        {
            return null;
        }

        // 查找对应的 <param name="xxx"> 标签
        var paramTag = $"<param name=\"{parameter.Name}\">";
        var paramStart = xmlComment!.IndexOf(paramTag);
        if (paramStart >= 0)
        {
            var contentStart = paramStart + paramTag.Length;
            var paramEnd = xmlComment.IndexOf("</param>", contentStart);
            if (paramEnd > contentStart)
            {
                var description = xmlComment.Substring(contentStart, paramEnd - contentStart).Trim();
                return EscapeForJsonString(description);
            }
        }

        return null;
    }

    /// <summary>
    /// 从 XML 注释中提取 summary 内容。
    /// </summary>
    private static string? ExtractSummaryFromXml(string xmlComment)
    {
        var summaryStart = xmlComment.IndexOf("<summary>");
        var summaryEnd = xmlComment.IndexOf("</summary>");
        if (summaryStart >= 0 && summaryEnd > summaryStart)
        {
            var summary = xmlComment.Substring(summaryStart + 9, summaryEnd - summaryStart - 9).Trim();
            return EscapeForJsonString(summary);
        }

        return null;
    }

    /// <summary>
    /// 转义 JSON 字符串中的特殊字符。
    /// </summary>
    private static string EscapeForJsonString(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
