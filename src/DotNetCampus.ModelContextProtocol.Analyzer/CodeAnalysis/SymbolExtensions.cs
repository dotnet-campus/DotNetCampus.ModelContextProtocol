using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.CodeAnalysis;

public static class SymbolExtensions
{
    private static readonly SymbolDisplayFormat SimpleContainingTypeFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    extension(ITypeSymbol typeSymbol)
    {
        /// <summary>
        /// 判断类型是否是可空类型（可空值类型或可空引用类型）。
        /// </summary>
        public bool IsNullableType
        {
            get
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
        }

        /// <summary>
        /// 获取不带命名空间的类型名称，包含嵌套类型。
        /// </summary>
        /// <returns>不带命名空间的类型名称。</returns>
        public string ToTypeOnlyString()
        {
            return typeSymbol.ToDisplayString(SimpleContainingTypeFormat);
        }
    }
}
