using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// 多态类型的信息，包含基类/接口和所有派生类型。
/// </summary>
public sealed class PolymorphicTypeInfo
{
    public PolymorphicTypeInfo(
        ITypeSymbol baseType,
        string discriminatorPropertyName,
        ImmutableArray<DerivedTypeInfo> derivedTypes)
    {
        BaseType = baseType;
        DiscriminatorPropertyName = discriminatorPropertyName;
        DerivedTypes = derivedTypes;
    }

    /// <summary>
    /// 基类或接口类型。
    /// </summary>
    public ITypeSymbol BaseType { get; }

    /// <summary>
    /// 类型鉴别器属性名称（对应 JsonPolymorphicAttribute.TypeDiscriminatorPropertyName）。
    /// </summary>
    public string DiscriminatorPropertyName { get; }

    /// <summary>
    /// 所有派生类型的信息列表。
    /// </summary>
    public ImmutableArray<DerivedTypeInfo> DerivedTypes { get; }

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

        var derivedTypes = ImmutableArray.CreateBuilder<DerivedTypeInfo>();

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
                    _ => null
                };
            }

            // 如果没有指定 discriminator，使用类型名称
            discriminator ??= derivedType.Name;

            derivedTypes.Add(new DerivedTypeInfo(derivedType, discriminator));
        }

        return new PolymorphicTypeInfo(typeSymbol, discriminatorPropertyName, derivedTypes.ToImmutable());
    }
}

/// <summary>
/// 派生类型的信息。
/// </summary>
public sealed class DerivedTypeInfo
{
    public DerivedTypeInfo(ITypeSymbol type, string discriminatorValue)
    {
        Type = type;
        DiscriminatorValue = discriminatorValue;
    }

    /// <summary>
    /// 派生类型。
    /// </summary>
    public ITypeSymbol Type { get; }

    /// <summary>
    /// 类型鉴别器的值。
    /// </summary>
    public string DiscriminatorValue { get; }
}
