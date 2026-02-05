using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当在 JSON 序列化上下文中未生成所需的 JsonTypeInfo 时，抛出此异常。
/// 用于特别提示开发者修复代码。
/// </summary>
public class McpToolJsonTypeInfoNotFoundException : McpToolException
{
    /// <summary>
    /// 初始化 <see cref="McpToolJsonTypeInfoNotFoundException"/> 类的新实例。
    /// </summary>
    /// <param name="jsonTypeName">类型名称</param>
    /// <param name="jsonTypeFullName">类型完整名称</param>
    public McpToolJsonTypeInfoNotFoundException(string jsonTypeName, string jsonTypeFullName) : base($"""
        The type "{jsonTypeFullName}" is not registered for deserialization.
        Add [JsonSerializable(typeof({jsonTypeName}))] to your JsonSerializerContext derived class.
        """)
    {
    }

    /// <summary>
    /// 初始化 <see cref="McpToolJsonTypeInfoNotFoundException"/> 类的新实例。
    /// </summary>
    /// <param name="jsonTypeName">类型名称</param>
    /// <param name="jsonTypeFullName">类型完整名称</param>
    /// <param name="jsonSerializerContextTypeName">JSON 序列化上下文类型名称</param>
    public McpToolJsonTypeInfoNotFoundException(string jsonTypeName, string jsonTypeFullName, string jsonSerializerContextTypeName) : base($"""
        The type "{jsonTypeFullName}" is not registered for deserialization.
        Add [JsonSerializable(typeof({jsonTypeName}))] to {jsonSerializerContextTypeName}.
        """)
    {
    }

    /// <summary>
    /// 确保在指定的 <see cref="JsonSerializerContext"/> 中存在所需的 <see cref="JsonTypeInfo{T}"/>。
    /// </summary>
    /// <param name="context">调用工具的上下文。</param>
    /// <param name="sourceGeneratedJsonTypeName">由源生成器提供的要被反序列化的类型名称。</param>
    /// <param name="sourceGeneratedJsonTypeFullName">由源生成器提供的要被反序列化的类型完整名称。</param>
    /// <typeparam name="T">要获取其 JsonTypeInfo 的类型。</typeparam>
    /// <returns>对应类型的 JsonTypeInfo。</returns>
    /// <exception cref="McpToolJsonTypeInfoNotFoundException">如果未生成所需的 JsonTypeInfo。</exception>
    public static JsonTypeInfo<T> EnsureTypeInfo<T>(IMcpServerCallToolContext context,
        string sourceGeneratedJsonTypeName, string sourceGeneratedJsonTypeFullName) =>
        context.JsonSerializerContext.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
        ?? (context.McpServer.Context.JsonSerializerTypeName is { } serializerName
            ? throw new McpToolJsonTypeInfoNotFoundException(sourceGeneratedJsonTypeName, sourceGeneratedJsonTypeFullName, serializerName)
            : throw new McpToolJsonTypeInfoNotFoundException(sourceGeneratedJsonTypeName, sourceGeneratedJsonTypeFullName));
}
