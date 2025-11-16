using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.CodeAnalysis;

internal class JsonSchemaTypeInfo
{
    public JsonSchemaTypeInfo(ITypeSymbol typeSymbol)
    {
        TypeSymbol = GetNotNullTypeSymbol(typeSymbol);
        SpecialKind = GetJsonTypeKind(typeSymbol);
        SchemaKind = SpecialKind.ToJsonSchemaType();
    }

    public ITypeSymbol TypeSymbol { get; }

    public JsonSpecialType SpecialKind { get; }

    public JsonSchemaType SchemaKind { get; }

    /// <summary>
    /// 获取当前类型是否可以从数组或列表赋值。
    /// </summary>
    /// <returns>如果可以从数组或列表赋值，则返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    public bool IsAssignableFromArrayOrList()
    {
        if (SpecialKind is not JsonSpecialType.Array)
        {
            return false;
        }

        if (TypeSymbol.Kind is SymbolKind.ArrayType)
        {
            return true;
        }

        var simpleName = TypeSymbol.ToSimpleDisplayString();
        return AllowedArrayOrListTypeNames.Contains(simpleName);
    }

    /// <summary>
    /// 如果当前类型是枚举类型，则返回其枚举类型符号，否则返回 <see langword="null"/>。
    /// </summary>
    /// <returns>枚举类型符号，或 <see langword="null"/>。</returns>
    public ITypeSymbol? AsEnumSymbol()
    {
        return SpecialKind is not JsonSpecialType.Enum ? null : TypeSymbol;
    }

    /// <summary>
    /// 如果当前类型是数组或集合类型，则返回其元素类型；否则返回 <see langword="null"/>。
    /// </summary>
    /// <returns></returns>
    public ITypeSymbol? AsArrayItemSymbol()
    {
        if (SpecialKind is not JsonSpecialType.Array)
        {
            return null;
        }

        if (TypeSymbol is IArrayTypeSymbol arrayTypeSymbol)
        {
            return arrayTypeSymbol.ElementType;
        }

        if (TypeSymbol is INamedTypeSymbol { TypeArguments.Length: 1 } namedTypeSymbol)
        {
            return namedTypeSymbol.TypeArguments[0];
        }

        return null;
    }

    /// <summary>
    /// 允许的单泛型类型名称。
    /// </summary>
    /// <summary>
    /// 允许的单泛型类型名称。
    /// </summary>
    private static readonly Dictionary<string, string> AllowedListTypeNames = new Dictionary<string, string>
    {
        ["Collection"] = "Collection",
        ["HashSet"] = "HashSet",
        ["ICollection"] = "List",
        ["IEnumerable"] = "List",
        ["IImmutableList"] = "ImmutableList",
        ["IImmutableSet"] = "ImmutableHashSet",
        ["IList"] = "List",
        ["ImmutableArray"] = "ImmutableArray",
        ["ImmutableHashSet"] = "ImmutableHashSet",
        ["ImmutableList"] = "ImmutableList",
        ["ImmutableSortedSet"] = "ImmutableSortedSet",
        ["IReadOnlyCollection"] = "List",
        ["IReadOnlyList"] = "List",
        ["ISet"] = "HashSet",
        ["List"] = "List",
        ["ReadOnlyCollection"] = "ReadOnlyCollection",
        ["SortedSet"] = "SortedSet",
    };

    /// <summary>
    /// 允许的双泛型类型名称。
    /// </summary>
    private static readonly Dictionary<string, string> AllowedDictionaryTypeNames = new Dictionary<string, string>
    {
        ["Dictionary"] = "Dictionary",
        ["IDictionary"] = "Dictionary",
        ["ImmutableDictionary"] = "ImmutableDictionary",
        ["ImmutableSortedDictionary"] = "ImmutableSortedDictionary",
        ["IReadOnlyDictionary"] = "Dictionary",
        ["KeyValuePair"] = "KeyValuePair",
        ["SortedDictionary"] = "SortedDictionary",
    };

    /// <summary>
    /// 允许的 RawArguments 泛型类型名称。
    /// </summary>
    private static readonly HashSet<string> AllowedArrayOrListTypeNames =
    [
        "IList", "IReadOnlyList", "ICollection", "IReadOnlyCollection", "IEnumerable",
    ];

    /// <summary>
    /// 视 <paramref name="typeSymbol"/> 为命令行属性的类型，按命令行属性的要求获取其所需的类型信息。<br/>
    /// 这个过程会丢掉类型的可空性信息。
    /// </summary>
    /// <param name="typeSymbol">类型符号。</param>
    /// <returns>类型信息。</returns>
    private static JsonSpecialType GetJsonTypeKind(ITypeSymbol typeSymbol)
    {
        var notNullTypeSymbol = GetNotNullTypeSymbol(typeSymbol);

        switch (notNullTypeSymbol.SpecialType)
        {
            case SpecialType.System_Boolean:
                return JsonSpecialType.Boolean;
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            // 不应支持这种不能跨进程传递的类型。
            // case SpecialType.System_IntPtr:
            // case SpecialType.System_UIntPtr:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                return JsonSpecialType.Integer;
            case SpecialType.System_Decimal:
            case SpecialType.System_Double:
            case SpecialType.System_Single:
                return JsonSpecialType.Number;
            case SpecialType.System_Char:
            case SpecialType.System_String:
                return JsonSpecialType.String;
            case SpecialType.System_Array:
            case SpecialType.System_Collections_IEnumerable:
            case SpecialType.System_Collections_Generic_IEnumerable_T:
            case SpecialType.System_Collections_Generic_IList_T:
            case SpecialType.System_Collections_Generic_ICollection_T:
            case SpecialType.System_Collections_IEnumerator:
            case SpecialType.System_Collections_Generic_IEnumerator_T:
            case SpecialType.System_Collections_Generic_IReadOnlyList_T:
            case SpecialType.System_Collections_Generic_IReadOnlyCollection_T:
                return JsonSpecialType.Array;
            case SpecialType.System_Object:
                return JsonSpecialType.Object;
            case SpecialType.None:
                // 其他类型，进行后续分析。
                break;
            default:
                return JsonSpecialType.Unknown;
        }

        if (notNullTypeSymbol.TypeKind is TypeKind.Enum)
        {
            return JsonSpecialType.Enum;
        }

        // List
        if (typeSymbol is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String })
        {
            return JsonSpecialType.Array;
        }

        // List
        if (notNullTypeSymbol is INamedTypeSymbol
            {
                TypeArguments: [{ SpecialType: SpecialType.System_String }],
                OriginalDefinition.Name: { } oneGenericName,
            } && AllowedListTypeNames.ContainsKey(oneGenericName))
        {
            return JsonSpecialType.Array;
        }

        // Dictionary
        if (notNullTypeSymbol is INamedTypeSymbol
            {
                TypeArguments: [{ SpecialType: SpecialType.System_String }, { SpecialType: SpecialType.System_String }],
                OriginalDefinition.Name: { } twoGenericName,
            } && AllowedDictionaryTypeNames.ContainsKey(twoGenericName))
        {
            return JsonSpecialType.Dictionary;
        }

        return JsonSpecialType.Object;
    }

    /// <summary>
    /// 如果 <paramref name="typeSymbol"/> 是可空值类型，则递归返回其基础类型，否则直接返回 <paramref name="typeSymbol"/> 本身。<br/>
    /// 不会处理其泛型参数的可空性。
    /// </summary>
    /// <param name="typeSymbol">要处理的类型符号。</param>
    /// <returns>基础类型符号。</returns>
    private static ITypeSymbol GetNotNullTypeSymbol(ITypeSymbol typeSymbol) => typeSymbol switch
    {
        INamedTypeSymbol
        {
            IsValueType: true,
            IsGenericType: true,
            OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
        } nullableTypeSymbol => nullableTypeSymbol.TypeArguments[0],
        _ => typeSymbol,
    };
}
