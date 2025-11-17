using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.CodeAnalysis;

public static class SymbolExtensions
{
    /// <summary>
    /// 用于将类型符号转换为仅包含名称的字符串形式。会去掉可空标记、命名空间、泛型参数等信息。
    /// </summary>
    private static readonly SymbolDisplayFormat SimpleDisplayFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.None,
        kindOptions: SymbolDisplayKindOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    /// <summary>
    /// 用于将类型符号转换为仅包含声明名称的字符串形式。会去掉可空标记、命名空间、泛型参数等信息，但不会使用类型关键字。
    /// </summary>
    private static readonly SymbolDisplayFormat SimpleDeclarationDisplayFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.None,
        kindOptions: SymbolDisplayKindOptions.None
    );

    /// <summary>
    /// 用于将类型符号转换为不带命名空间的类型名称字符串形式，包含嵌套类型。
    /// </summary>
    private static readonly SymbolDisplayFormat SimpleContainingDisplayFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    /// <summary>
    /// 用于将类型符号转换为不带可空标记的全局类型名称字符串形式，包含命名空间和嵌套类型。
    /// </summary>
    private static readonly SymbolDisplayFormat NullableDisabledGlobalDisplayFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    extension(ITypeSymbol typeSymbol)
    {
        /// <summary>
        /// 判断类型是否是可空类型（可空值类型或可空引用类型）。
        /// </summary>
        public bool IsNullableType => typeSymbol switch
        {
            INamedTypeSymbol
            {
                IsValueType: true,
                IsGenericType: true,
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
            } => true,
            { NullableAnnotation: NullableAnnotation.Annotated } => true,
            _ => false,
        };

        /// <summary>
        /// 判断类型是否是可空值类型。
        /// </summary>
        public bool IsNullableValueType => typeSymbol switch
        {
            INamedTypeSymbol
            {
                IsValueType: true,
                IsGenericType: true,
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
            } => true,
            _ => false,
        };

        /// <summary>
        /// 如果 <paramref name="typeSymbol"/> 是可空值类型，则递归返回其基础类型，否则直接返回 <paramref name="typeSymbol"/> 本身。<br/>
        /// 不会处理其泛型参数的可空性。
        /// </summary>
        /// <returns>基础类型符号。</returns>
        public ITypeSymbol GetNotNullTypeSymbol() => typeSymbol switch
        {
            INamedTypeSymbol
            {
                IsValueType: true,
                IsGenericType: true,
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
            } nullableTypeSymbol => nullableTypeSymbol.TypeArguments[0],
            _ => typeSymbol,
        };

        /// <summary>
        /// 获取类型的简单名称，仅包含名称本身，不包含命名空间、泛型参数、可空标记等信息，尽可能使用类型关键字。
        /// </summary>
        /// <returns></returns>
        public string ToSimpleDisplayString() => typeSymbol.ToDisplayString(SimpleDisplayFormat);

        /// <summary>
        /// 获取类型的简单名称，仅包含名称本身，不包含命名空间、泛型参数、可空标记等信息，尽可能使用类型名称而不是关键字。
        /// </summary>
        /// <returns></returns>
        public string ToDeclarationDisplayString() => typeSymbol.ToDisplayString(SimpleDeclarationDisplayFormat);

        /// <summary>
        /// 获取不带命名空间的类型名称，包含嵌套类型。
        /// </summary>
        /// <returns></returns>
        public string ToDeclarationNestedDisplayString() => typeSymbol.ToDisplayString(SimpleContainingDisplayFormat);

        /// <summary>
        /// 获取类型的全局名称字符串形式，去掉可空标记。
        /// </summary>
        /// <returns></returns>
        public string ToNullableDisabledGlobalDisplayString() => typeSymbol.ToDisplayString(NullableDisabledGlobalDisplayFormat);
    }
}
