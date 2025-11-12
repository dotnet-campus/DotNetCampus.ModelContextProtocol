using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.CodeAnalysis;

/// <summary>
/// JSON Schema 支持的数据类型。
/// </summary>
internal enum JsonSchemaType
{
    /// <summary>
    /// 未定义。
    /// </summary>
    Undefined,

    /// <summary>
    /// 数组。
    /// </summary>
    Array,

    /// <summary>
    /// 布尔值。
    /// </summary>
    Boolean,

    /// <summary>
    /// 空。
    /// </summary>
    Null,

    /// <summary>
    /// 数值。
    /// </summary>
    Number,

    /// <summary>
    /// 对象。
    /// </summary>
    Object,

    /// <summary>
    /// 字符串。
    /// </summary>
    String,
}

/// <summary>
/// 比 JSON Schema 更准确一些的特殊类型。
/// </summary>
internal enum JsonSpecialType
{
    /// <summary>
    /// 未知类型。
    /// </summary>
    Unknown,

    /// <summary>
    /// 布尔值。
    /// </summary>
    Boolean,

    /// <summary>
    /// 数值。
    /// </summary>
    Number,

    /// <summary>
    /// 枚举。
    /// </summary>
    Enum,

    /// <summary>
    /// 字符串。
    /// </summary>
    String,

    /// <summary>
    /// 数组。
    /// </summary>
    Array,

    /// <summary>
    /// 字典。
    /// </summary>
    Dictionary,

    /// <summary>
    /// 对象。
    /// </summary>
    Object,
}

internal static class JsonSchemaTypeInfoExtensions
{
    extension(JsonSpecialType specialType)
    {
        /// <summary>
        /// 将特殊类型映射为 JSON 模式的类型。
        /// </summary>
        /// <returns>JSON 模式的类型。</returns>
        public JsonSchemaType ToJsonSchemaType() => specialType switch
        {
            JsonSpecialType.Unknown => JsonSchemaType.Undefined,
            JsonSpecialType.Boolean => JsonSchemaType.Boolean,
            JsonSpecialType.Number => JsonSchemaType.Number,
            JsonSpecialType.Enum => JsonSchemaType.String,
            JsonSpecialType.String => JsonSchemaType.String,
            JsonSpecialType.Array => JsonSchemaType.Array,
            JsonSpecialType.Dictionary => JsonSchemaType.Object,
            JsonSpecialType.Object => JsonSchemaType.Object,
            _ => JsonSchemaType.Undefined,
        };
    }

    /// <param name="typeSymbol">类型符号。</param>
    extension(ITypeSymbol typeSymbol)
    {
        /// <summary>
        /// 将类型符号映射为 JSON 模式的类型。
        /// </summary>
        /// <returns>JSON 模式的类型。</returns>
        public JsonSchemaType ToJsonSchemaType()
        {
            return new JsonSchemaTypeInfo(typeSymbol).SchemaKind;
        }

        /// <summary>
        /// 将类型符号映射为 JSON 模式的类型。
        /// </summary>
        /// <returns>JSON 模式的类型。</returns>
        public string ToJsonSchemaTypeString()
        {
            return new JsonSchemaTypeInfo(typeSymbol).SchemaKind.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// 将类型符号映射为 JSON 模式的类型。
        /// </summary>
        /// <returns>JSON 模式的类型。</returns>
        public JsonSchemaTypeInfo ToJsonSchemaTypeInfo()
        {
            return new JsonSchemaTypeInfo(typeSymbol);
        }
    }
}
