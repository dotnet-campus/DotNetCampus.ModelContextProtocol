using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using Microsoft.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Utils;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;
using JsonType = DotNetCampus.ModelContextProtocol.CodeAnalysis.JsonSchemaType;

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
    /// 属性类型是否是可空值类型（Nullable&lt;T&gt;）或可空引用类型。
    /// </summary>
    public bool IsNullableType { get; init; }

    /// <summary>
    /// 描述文本（已转义）。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 是否是必需赋值的属性。
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// 属性的默认值，以 JsonElement 表示的表达式。
    /// </summary>
    public string? DefaultValueJsonElement { get; init; }

    /// <summary>
    /// 对于顶级 MCP 工具来说，属性是由方法的参数决定的，会给此属性显式赋值。
    /// 而其他类型此属性为 <see langword="null"/>，需要通过 <see cref="GetProperties"/> 方法获取。
    /// </summary>
    public IReadOnlyList<JsonPropertySchemaInfo>? Properties { get; init; }

    /// <summary>
    /// 如果当前类型是多态类型，则此属性包含多态类型信息；否则为 <see langword="null"/>。
    /// </summary>
    public PolymorphicTypeInfo? PolymorphicInfo { get; init; }

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

        // 如果是任意 JSON 元素类型（JsonElement、JsonNode 等），不需要展开属性
        if (PropertyType.IsAnyJsonElementType())
        {
            return [];
        }

        if (PropertyType.ToJsonSchemaTypeInfo().SpecialKind is not JsonSpecialType.Object)
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

        var a = new[] { "" };
        return properties.ToArray();
    }

    public string GetJsonSchemaTypeExpression() => (PropertyType.IsAnyJsonElementType(), IsNullableType) switch
    {
        (true, _) => $"{G.JsonSerializer}.SerializeToElement(new[] {{ \"string\", \"number\", \"boolean\", \"object\", \"null\" }}, jsonContext.StringArray)",
        (_, true) => $"{G.JsonSerializer}.SerializeToElement(new[] {{ \"{JsonSchemaType}\", \"null\" }}, jsonContext.StringArray)",
        (_, false) => $"{G.JsonSerializer}.SerializeToElement(\"{JsonSchemaType}\", jsonContext.String)",
    };

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

        var itemType = PropertyType.ToJsonSchemaTypeInfo().AsArrayItemSymbol();
        return itemType is null
            ? null
            : From(itemType, JsonPropertyName);
    }

    /// <summary>
    /// 获取多态类型的所有派生类型的 Schema 列表。
    /// </summary>
    public IReadOnlyList<JsonPropertySchemaInfo> GetPolymorphicDerivedTypes()
    {
        if (PolymorphicInfo is null)
        {
            return [];
        }

        return PolymorphicInfo.DerivedTypes
            .Select(d => From(d.Type, JsonPropertyName) with
            {
                // 为每个派生类型添加鉴别器属性约束
                PolymorphicInfo = null, // 派生类型不需要多态信息
            })
            .ToList();
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
        // 检查是否有 InputObject 类型的参数
        var inputObjectParam = model.Method.Parameters.FirstOrDefault(p => p.GetParameterType() == ToolParameterType.InputObject);
        if (inputObjectParam != null)
        {
            // 如果有 InputObject 参数，直接使用该参数类型的 Schema 信息（保留多态信息）
            return From(inputObjectParam.Type, model.Name);
        }

        return new JsonPropertySchemaInfo(model.ContainingType)
        {
            JsonPropertyName = model.Name, // 仅为结构相同，实际不会被使用。
            JsonSchemaType = "object", // 顶层模型一定是对象。
            IsNullableType = false, // 顶层模型一定不是可空值类型。
            Description = null, // 顶层模型有其他描述位置。
            IsRequired = true, //  顶层模型一定是必需的。
            DefaultValueJsonElement = null, // 顶层模型没有默认值。
            Properties = model.GetProperties(),
        };
    }

    /// <summary>
    /// 从属性符号创建 Schema 属性信息。
    /// </summary>
    public static JsonPropertySchemaInfo From(IPropertySymbol property)
    {
        var isNullable = property.Type.IsNullableValueType ||
                         (!property.Type.IsValueType && property.Type.NullableAnnotation == NullableAnnotation.Annotated);

        return new JsonPropertySchemaInfo(property.Type)
        {
            JsonPropertyName = NamingHelper.MakeCamelCase(property.Name),
            JsonSchemaType = property.Type.ToJsonSchemaTypeString(),
            IsNullableType = property.Type.IsNullableType,
            Description = property.GetSummaryFromSymbol(),
            IsRequired = property.IsRequired && !isNullable, // 可空类型不是必需的
            DefaultValueJsonElement = null, // 暂时不知道如何获取属性的默认值。
            PolymorphicInfo = PolymorphicTypeInfo.FromTypeSymbol(property.Type),
        };
    }

    /// <summary>
    /// 从构造函数参数创建 Schema 属性信息。
    /// </summary>
    public static JsonPropertySchemaInfo From(IParameterSymbol parameter)
    {
        return new JsonPropertySchemaInfo(parameter.Type)
        {
            JsonPropertyName = parameter.GetJsonPropertyName(),
            JsonSchemaType = parameter.Type.ToJsonSchemaTypeString(),
            IsNullableType = parameter.Type.IsNullableType,
            Description = parameter.GetParameterDescriptionWithAttribute(),
            IsRequired = !parameter.HasExplicitDefaultValue,
            DefaultValueJsonElement = GetJsonSchemaDefaultValueExpression(parameter),
            PolymorphicInfo = PolymorphicTypeInfo.FromTypeSymbol(parameter.Type),
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
            IsNullableType = typeSymbol.IsNullableType,
            Description = null,
            IsRequired = true,
            DefaultValueJsonElement = null,
            PolymorphicInfo = PolymorphicTypeInfo.FromTypeSymbol(typeSymbol),
        };
    }

    private static string? GetJsonSchemaDefaultValueExpression(IParameterSymbol parameter)
    {
        var info = parameter.Type.ToJsonSchemaTypeInfo();
        var hasDefaultValue = parameter.HasExplicitDefaultValue;
        var defaultValue = hasDefaultValue ? parameter.ExplicitDefaultValue : null;
        if (!hasDefaultValue)
        {
            return null;
        }
        return (defaultValue, info.SchemaKind) switch
        {
            (null, _) => $"{G.JsonDocument}.Parse(\"null\").RootElement",
            (_, JsonType.Boolean) => $"{G.JsonSerializer}.SerializeToElement({defaultValue.ToString().ToLowerInvariant()}, jsonContext.Boolean)",
            (_, JsonType.Integer) => $"{G.JsonSerializer}.SerializeToElement((long){defaultValue}, jsonContext.Int64)",
            (_, JsonType.Number) => $"{G.JsonSerializer}.SerializeToElement((decimal){defaultValue}, jsonContext.Decimal)",
            (_, JsonType.String) => $"{G.JsonSerializer}.SerializeToElement(\"{defaultValue}\", jsonContext.String)",
            // 其他情况，C# 语法中写不出来默认值。
            _ => null,
        };
    }
}
