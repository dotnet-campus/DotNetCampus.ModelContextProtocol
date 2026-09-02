using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// 枚举值信息。
/// </summary>
public readonly record struct EnumJsonValueInfo
{
    /// <summary>
    /// 运行时枚举成员名称。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 枚举成员在 JSON wire format 中的名称。
    /// </summary>
    public required string JsonName { get; init; }

    /// <summary>
    /// 枚举成员的底层常量值。
    /// </summary>
    public object? ConstantValue { get; init; }

    /// <summary>
    /// 枚举值描述（文档注释或 [Description] 特性，后者优先）。
    /// </summary>
    public string? Description { get; init; }

    public static IEnumerable<EnumJsonValueInfo> FromEnumSymbol(ITypeSymbol enumTypeSymbol)
    {
        if (enumTypeSymbol.ToJsonSchemaTypeInfo().AsEnumSymbol() is not INamedTypeSymbol enumSymbol)
        {
            yield break;
        }

        foreach (var field in enumSymbol.GetMembers().OfType<IFieldSymbol>().Where(x => x.IsConst))
        {
            yield return new EnumJsonValueInfo
            {
                Name = field.Name,
                JsonName = field.GetJsonStringEnumMemberName() ?? field.Name,
                ConstantValue = field.ConstantValue,
                Description = field.GetSummaryFromSymbol(),
            };
        }
    }
}

file static class EnumFieldSymbolExtensions
{
    public static string? GetJsonStringEnumMemberName(this IFieldSymbol field)
    {
        var attribute = field.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonStringEnumMemberNameAttribute");

        return attribute?.ConstructorArguments is { Length: > 0 } args && args[0].Value is string name
            ? name
            : null;
    }
}
