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
    /// 枚举值名称。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 枚举值描述（文档注释或 [Description] 特性，后者优先）。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 获取枚举值在 Json 中的名称（camelCase）。
    /// </summary>
    /// <returns></returns>
    public string GetJsonName()
    {
        return NamingHelper.MakeCamelCase(Name);
    }

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
                Description = field.GetSummaryFromSymbol(),
            };
        }
    }
}
