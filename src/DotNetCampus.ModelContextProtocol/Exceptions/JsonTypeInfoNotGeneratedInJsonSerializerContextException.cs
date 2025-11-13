using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当在 JSON 序列化上下文中未生成所需的 JsonTypeInfo 时，抛出此异常。
/// 用于特别提示开发者修复代码。
/// </summary>
public class JsonTypeInfoNotGeneratedInJsonSerializerContextException : ModelContextProtocolException
{
    public JsonTypeInfoNotGeneratedInJsonSerializerContextException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 确保在指定的 <see cref="JsonSerializerContext"/> 中存在所需的 <see cref="JsonTypeInfo{T}"/>。
    /// </summary>
    /// <param name="jsonSerializerContext">Json 序列化上下文。</param>
    /// <typeparam name="T">要获取其 JsonTypeInfo 的类型。</typeparam>
    /// <returns>对应类型的 JsonTypeInfo。</returns>
    /// <exception cref="JsonTypeInfoNotGeneratedInJsonSerializerContextException">如果未生成所需的 JsonTypeInfo。</exception>
    public static JsonTypeInfo<T> EnsureTypeInfo<T>(JsonSerializerContext jsonSerializerContext) =>
        jsonSerializerContext.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
        ?? throw new JsonTypeInfoNotGeneratedInJsonSerializerContextException(
            $"Please add [JsonSerializable(typeof({typeof(T).FullName}))] to your JsonSerializerContext derived class.");
}
