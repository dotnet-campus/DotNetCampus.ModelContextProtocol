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
}
