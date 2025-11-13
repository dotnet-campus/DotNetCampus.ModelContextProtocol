using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.CodeAnalysis;

public static class SymbolExtensions
{
    /// <summary>
    /// 用于将类型符号转换为仅包含名称的字符串形式。会去掉可空标记、命名空间、泛型参数等信息。
    /// </summary>
    private static readonly SymbolDisplayFormat SimpleNameFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.None,
        kindOptions: SymbolDisplayKindOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    /// <summary>
    /// 用于将类型符号转换为仅包含声明名称的字符串形式。会去掉可空标记、命名空间、泛型参数等信息，但不会使用类型关键字。
    /// </summary>
    private static readonly SymbolDisplayFormat SimpleDeclarationNameFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.None,
        kindOptions: SymbolDisplayKindOptions.None
    );

    /// <summary>
    /// 用于将类型符号转换为不带命名空间的类型名称字符串形式，包含嵌套类型。
    /// </summary>
    private static readonly SymbolDisplayFormat SimpleContainingNameFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

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
        /// 获取类型的简单名称，仅包含名称本身，不包含命名空间、泛型参数、可空标记等信息，尽可能使用类型关键字。
        /// </summary>
        /// <returns></returns>
        public string ToSimpleDisplayString() => typeSymbol.ToDisplayString(SimpleNameFormat);

        /// <summary>
        /// 获取类型的简单名称，仅包含名称本身，不包含命名空间、泛型参数、可空标记等信息，尽可能使用类型名称而不是关键字。
        /// </summary>
        /// <returns></returns>
        public string ToDeclarationDisplayString() => typeSymbol.ToDisplayString(SimpleDeclarationNameFormat);

        /// <summary>
        /// 获取不带命名空间的类型名称，包含嵌套类型。
        /// </summary>
        /// <returns></returns>
        public string ToDeclarationNestedDisplayString() => typeSymbol.ToDisplayString(SimpleContainingNameFormat);
    }

    /// <summary>
    /// 判断参数是否为 CancellationToken 类型。
    /// </summary>
    public static bool IsCancellationTokenParameter(this IParameterSymbol parameter)
    {
        return parameter.Type.ToGlobalDisplayString() == "global::System.Threading.CancellationToken";
    }
}
