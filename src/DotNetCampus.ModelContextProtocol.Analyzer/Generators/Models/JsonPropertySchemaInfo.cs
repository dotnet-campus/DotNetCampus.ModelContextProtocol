using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using Microsoft.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Utils;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// 一个 Json 属性的 Schema 信息和其关联的 .NET 类型。
/// </summary>
/// <param name="PropertyType">属性的 .NET 类型。</param>
public record JsonPropertySchemaInfo(ITypeSymbol PropertyType)
{
    /// <summary>
    /// JSON 属性名（kebab-case）。
    /// </summary>
    public required string JsonPropertyName { get; init; }

    /// <summary>
    /// JSON Schema 类型表示。
    /// </summary>
    public required string JsonSchemaType { get; init; }

    /// <summary>
    /// 描述文本（已转义）。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 是否是必需赋值的属性。
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// 属性的默认值（如果有）。
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// 对于顶级 MCP 工具来说，属性是由方法的参数决定的，会给此属性显式赋值。
    /// 而其他类型此属性为 <see langword="null"/>，需要通过 <see cref="GetProperties"/> 方法获取。
    /// </summary>
    public IReadOnlyList<JsonPropertySchemaInfo>? Properties { get; init; }

    /// <summary>
    /// 获取属性类型的所有公共属性的 Schema 信息。
    /// </summary>
    public IReadOnlyList<JsonPropertySchemaInfo> GetProperties()
    {
        if (Properties is { } p)
        {
            return p;
        }

        if (PropertyType is not INamedTypeSymbol typeSymbol)
        {
            return [];
        }

        var properties = new List<JsonPropertySchemaInfo>();

        // 获取所有公共属性
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false } property)
            {
                properties.Add(From(property));
            }
        }

        // 处理主构造函数参数。
        if (typeSymbol.IsRecord)
        {
            foreach (var ctor in typeSymbol.Constructors)
            {
                if (ctor.DeclaredAccessibility == Accessibility.Public)
                {
                    foreach (var param in ctor.Parameters)
                    {
                        var jsonName = NamingHelper.MakeKebabCase(param.Name, true, true);
                        // 检查是否已经通过属性添加
                        if (properties.All(p => p.JsonPropertyName != jsonName))
                        {
                            properties.Add(From(param));
                        }
                    }
                    // 只处理第一个公共构造函数
                    break;
                }
            }
        }

        return properties.ToArray();
    }

    /// <summary>
    /// 获取枚举类型的所有可能值的集合表达式语法代码。
    /// </summary>
    /// <returns>枚举值集合的表达式语法代码。</returns>
    public string? GetJsonEnumNameExpressionOrDefault()
    {
        if (Properties is not null)
        {
            return null;
        }

        var enums = EnumJsonValueInfo.FromEnumSymbol(PropertyType).ToList();
        return enums.Count is 0
            ? null
            : $"[ {string.Join(", ", enums.Select(ev => $"\"{ev.Name}\""))} ]";
    }

    /// <summary>
    /// 如果属性类型是数组或集合类型，则获取其元素类型的 Schema 信息；否则返回 <see langword="null"/>。
    /// </summary>
    /// <returns></returns>
    public JsonPropertySchemaInfo? GetItemSchemaOfArrayOrDefault()
    {
        if (Properties is not null)
        {
            return null;
        }

        var itemType = new JsonSchemaTypeInfo(PropertyType).AsArrayItemSymbol();
        return itemType is null
            ? null
            : From(itemType, JsonPropertyName);
    }

    /// <summary>
    /// 获取所有必需属性的集合表达式语法代码。
    /// </summary>
    public string? GetJsonRequiredPropertiesExpressionOrDefault()
    {
        var names = GetProperties()
            .Where(m => m.IsRequired)
            .ToList();

        return names.Count is 0
            ? null
            : $"[ {string.Join(", ", names.Select(x => $"\"{x.JsonPropertyName}\""))} ]";
    }

    /// <summary>
    /// 从方法参数创建 Schema 属性信息。
    /// </summary>
    public static JsonPropertySchemaInfo From(McpServerToolGeneratingModel model)
    {
        return new JsonPropertySchemaInfo(model.ContainingType)
        {
            JsonPropertyName = model.Name, // 仅为结构相同，实际不会被使用。
            JsonSchemaType = "object", // 顶层模型一定是对象。
            Description = model.Description,
            IsRequired = true,
            DefaultValue = null,
            Properties = model.Method.Parameters.Select(From).ToList(),
        };
    }

    /// <summary>
    /// 从编译时模型参数创建 Schema 属性信息。
    /// </summary>
    public static JsonPropertySchemaInfo From(ToolParameterModel parameter)
    {
        return new JsonPropertySchemaInfo(parameter.Type)
        {
            JsonPropertyName = parameter.JsonName,
            JsonSchemaType = parameter.Type.ToJsonSchemaTypeString(),
            Description = parameter.GetJsonEscapedDescription(),
            IsRequired = parameter.IsRequired,
            DefaultValue = parameter.DefaultValue?.ToString(),
        };
    }

    /// <summary>
    /// 从属性符号创建 Schema 属性信息。
    /// </summary>
    public static JsonPropertySchemaInfo From(IPropertySymbol property)
    {
        return new JsonPropertySchemaInfo(property.Type)
        {
            JsonPropertyName = NamingHelper.MakeCamelCase(property.Name),
            JsonSchemaType = property.Type.ToJsonSchemaTypeString(),
            Description = property.GetSummaryFromSymbol(),
            IsRequired = property.IsRequired || property.Type.IsNullableType,
            DefaultValue = null,
        };
    }

    /// <summary>
    /// 从构造函数参数创建 Schema 属性信息。
    /// </summary>
    public static JsonPropertySchemaInfo From(IParameterSymbol parameter)
    {
        return new JsonPropertySchemaInfo(parameter.Type)
        {
            JsonPropertyName = NamingHelper.MakeCamelCase(parameter.Name),
            JsonSchemaType = parameter.Type.ToJsonSchemaTypeString(),
            Description = parameter.GetParameterDescription(),
            IsRequired = parameter.Type.IsNullableType || !parameter.HasExplicitDefaultValue,
            // DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue?.ToString() : null,
        };
    }

    /// <summary>
    /// 从构造函数参数创建 Schema 属性信息。
    /// </summary>
    public static JsonPropertySchemaInfo From(ITypeSymbol typeSymbol, string propertyName)
    {
        return new JsonPropertySchemaInfo(typeSymbol)
        {
            JsonPropertyName = NamingHelper.MakeCamelCase(propertyName),
            JsonSchemaType = typeSymbol.ToJsonSchemaTypeString(),
            Description = null,
            IsRequired = true,
            DefaultValue = null,
        };
    }

    // /// <summary>
    // /// 解包可空类型，返回核心类型和是否可空。
    // /// </summary>
    // public static (ITypeSymbol CoreType, bool IsNullable) UnwrapNullableType(ITypeSymbol typeSymbol)
    // {
    //     // 处理 Nullable<T> (值类型可空)
    //     if (typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } namedType)
    //     {
    //         return (namedType.TypeArguments[0], true);
    //     }
    //
    //     // 处理引用类型可空注解
    //     if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
    //     {
    //         return (typeSymbol.WithNullableAnnotation(NullableAnnotation.NotAnnotated), true);
    //     }
    //
    //     return (typeSymbol, false);
    // }
    //
    // /// <summary>
    // /// 获取数组或集合的元素类型。
    // /// </summary>
    // public static ITypeSymbol? GetArrayElementType(ITypeSymbol typeSymbol)
    // {
    //     // 处理数组类型
    //     if (typeSymbol is IArrayTypeSymbol arrayType)
    //     {
    //         return arrayType.ElementType;
    //     }
    //
    //     // 处理泛型集合类型
    //     if (typeSymbol is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
    //     {
    //         var fullName = namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    //         if (fullName.StartsWith("global::System.Collections.Generic.IEnumerable<") ||
    //             fullName.StartsWith("global::System.Collections.Generic.List<") ||
    //             fullName.StartsWith("global::System.Collections.Generic.IList<") ||
    //             fullName.StartsWith("global::System.Collections.Generic.IReadOnlyList<") ||
    //             fullName.StartsWith("global::System.Collections.Generic.ICollection<"))
    //         {
    //             return namedType.TypeArguments[0];
    //         }
    //     }
    //
    //     return null;
    // }
    //
    // /// <summary>
    // /// 判断类型是否为枚举类型。
    // /// </summary>
    // public static bool IsEnumType(ITypeSymbol typeSymbol)
    // {
    //     return typeSymbol.TypeKind == TypeKind.Enum;
    // }
    //
    // /// <summary>
    // /// 获取枚举的所有值和描述。
    // /// </summary>
    // public static EnumJsonValueInfo[] GetEnumValues(ITypeSymbol typeSymbol)
    // {
    //     if (typeSymbol is not INamedTypeSymbol namedType || namedType.TypeKind != TypeKind.Enum)
    //     {
    //         return Array.Empty<EnumJsonValueInfo>();
    //     }
    //
    //     var values = new List<EnumJsonValueInfo>();
    //     foreach (var member in namedType.GetMembers())
    //     {
    //         if (member is IFieldSymbol { IsConst: true } field)
    //         {
    //             var value = field.Name;
    //             var description = GetEnumMemberDescription(field);
    //             values.Add(new EnumJsonValueInfo(value, description));
    //         }
    //     }
    //
    //     return values.ToArray();
    // }
    //
    // /// <summary>
    // /// 获取枚举成员的描述。
    // /// </summary>
    // private static string? GetEnumMemberDescription(IFieldSymbol field)
    // {
    //     return field.GetSummaryFromSymbol();
    // }
}
