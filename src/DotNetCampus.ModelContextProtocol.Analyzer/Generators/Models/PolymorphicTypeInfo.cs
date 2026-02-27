using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// 多态类型的信息，包含基类/接口和所有派生类型。
/// </summary>
public sealed class PolymorphicTypeInfo(
    ITypeSymbol baseType,
    string discriminatorPropertyName,
    IReadOnlyList<DerivedTypeInfo> derivedTypes)
{
    /// <summary>
    /// 基类或接口类型。
    /// </summary>
    public ITypeSymbol BaseType { get; } = baseType;

    /// <summary>
    /// 类型鉴别器属性名称，即 [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")] 中的 type。
    /// </summary>
    public string DiscriminatorPropertyName { get; } = discriminatorPropertyName;

    /// <summary>
    /// 所有派生类型的信息列表。
    /// </summary>
    public IReadOnlyList<DerivedTypeInfo> DerivedTypes { get; } = derivedTypes;

    /// <summary>
    /// 从类型符号中提取多态信息，如果不是多态类型则返回 null。
    /// </summary>
    public static PolymorphicTypeInfo? FromTypeSymbol(ITypeSymbol typeSymbol)
    {
        // 查找 JsonPolymorphicAttribute
        var polymorphicAttr = typeSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonPolymorphicAttribute");

        if (polymorphicAttr is null)
        {
            return null;
        }

        // 提取 TypeDiscriminatorPropertyName
        var discriminatorPropertyName = polymorphicAttr.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == "TypeDiscriminatorPropertyName")
            .Value.Value as string ?? "$type";

        // 查找所有 JsonDerivedTypeAttribute
        var derivedTypeAttrs = typeSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonDerivedTypeAttribute")
            .ToList();

        if (derivedTypeAttrs.Count == 0)
        {
            return null;
        }

        var derivedTypes = new List<DerivedTypeInfo>();

        foreach (var attr in derivedTypeAttrs)
        {
            // 第一个构造函数参数是派生类型
            if (attr.ConstructorArguments.Length == 0 || attr.ConstructorArguments[0].Value is not ITypeSymbol derivedType)
            {
                continue;
            }

            // 第二个构造函数参数（如果有）是 typeDiscriminator
            string? discriminator = null;
            if (attr.ConstructorArguments.Length >= 2)
            {
                discriminator = attr.ConstructorArguments[1].Value switch
                {
                    string s => s,
                    int i => i.ToString(),
                    _ => null,
                };
            }

            // 如果没有指定 discriminator，使用类型名称
            discriminator ??= derivedType.Name;

            derivedTypes.Add(new DerivedTypeInfo(derivedType, discriminator));
        }

        return new PolymorphicTypeInfo(typeSymbol, discriminatorPropertyName, derivedTypes);
    }
}

/// <summary>
/// 派生类型的信息。
/// </summary>
public sealed class DerivedTypeInfo(ITypeSymbol type, string discriminatorValue)
{
    /// <summary>
    /// 派生类型。
    /// </summary>
    public ITypeSymbol Type { get; } = type;

    /// <summary>
    /// 类型鉴别器的值。
    /// </summary>
    public string DiscriminatorValue { get; } = discriminatorValue;
}
