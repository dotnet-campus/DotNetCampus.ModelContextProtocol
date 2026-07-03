using Microsoft.CodeAnalysis;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

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

            // 第二个构造函数参数（如果有）是 typeDiscriminator。没有显式 discriminator 时，System.Text.Json 不会使用派生类型名作为 wire value。
            if (attr.ConstructorArguments.Length < 2)
            {
                continue;
            }

            var discriminator = attr.ConstructorArguments[1].Value switch
            {
                string s => new DerivedTypeDiscriminator(s),
                int i => new DerivedTypeDiscriminator(i),
                _ => null,
            };

            if (discriminator is null)
            {
                continue;
            }

            derivedTypes.Add(new DerivedTypeInfo(derivedType, discriminator));
        }

        return derivedTypes.Count == 0
            ? null
            : new PolymorphicTypeInfo(typeSymbol, discriminatorPropertyName, derivedTypes);
    }
}

/// <summary>
/// 派生类型的信息。
/// </summary>
public sealed class DerivedTypeInfo(ITypeSymbol type, DerivedTypeDiscriminator discriminator)
{
    /// <summary>
    /// 派生类型。
    /// </summary>
    public ITypeSymbol Type { get; } = type;

    public DerivedTypeDiscriminator Discriminator { get; } = discriminator;

    /// <summary>
    /// 类型鉴别器的显示值。
    /// </summary>
    public string DiscriminatorValue => Discriminator.DisplayValue;
}

public sealed class DerivedTypeDiscriminator
{
    public DerivedTypeDiscriminator(string value)
    {
        StringValue = value;
        DisplayValue = value;
    }

    public DerivedTypeDiscriminator(int value)
    {
        Int32Value = value;
        DisplayValue = value.ToString();
    }

    public string? StringValue { get; }

    public int? Int32Value { get; }

    public string DisplayValue { get; }

    public string ToJsonElementExpression() => StringValue is { } stringValue
        ? $"{G.JsonSerializer}.SerializeToElement(\"{stringValue}\", jsonContext.String)"
        : $"{G.JsonSerializer}.SerializeToElement({Int32Value!.Value}, jsonContext.Int32)";
}
