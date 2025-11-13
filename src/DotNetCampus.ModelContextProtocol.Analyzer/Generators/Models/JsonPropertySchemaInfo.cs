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
        if (Properties is not null)
        {
            // 顶级 MCP 工具是通过方法的参数列表视作属性来工作的。
            return Properties;
        }

        if (new JsonSchemaTypeInfo(PropertyType).SpecialKind is not JsonSpecialType.Object)
        {
            // 非对象类型没有属性。
            return [];
        }

        if (PropertyType is not INamedTypeSymbol typeSymbol)
        {
            // 大部分情况应该都被前面的 object 类型判断覆盖了，这里作为兜底。
            return [];
        }

        var properties = new List<JsonPropertySchemaInfo>();

        // 获取所有公共属性。
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
            foreach (var param in typeSymbol.Constructors
                         .Where(ctor => ctor.DeclaredAccessibility == Accessibility.Public)
                         .SelectMany(x => x.Parameters))
            {
                var jsonName = NamingHelper.MakeCamelCase(param.Name);
                // 检查是否已经通过属性添加。
                if (properties.All(p => p.JsonPropertyName != jsonName))
                {
                    properties.Add(From(param));
                }
                // 只处理第一个公共构造函数。
                break;
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
            Properties = model.GetProperties(),
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
            IsRequired = !parameter.HasExplicitDefaultValue,
            DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue?.ToString() : null,
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
}
