using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// MCP 服务返回的工具调用结果，包含一个可以被延迟序列化的结果。
/// </summary>
/// <param name="Result">要包含的结果。</param>
/// <typeparam name="T">结果的类型。</typeparam>
public sealed record CallToolResult<T>([property: JsonIgnore] T Result) : CallToolResult, ICallToolResultJsonSerializer
{
    /// <summary>
    /// 获取一个用了延迟序列化结果的工厂方法。
    /// </summary>
    [JsonIgnore]
    public required Func<T, JsonTypeInfo<T>, CallToolResult> ResultFactory { get; init; }

    /// <inheritdoc />
    public CallToolResult SerializeToCallToolResult(JsonSerializerContext jsonSerializerContext)
    {
        if (jsonSerializerContext.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> jsonTypeInfo)
        {
            return ResultFactory(Result, jsonTypeInfo);
        }
        throw new McpToolJsonTypeInfoNotFoundException(typeof(T).Name, typeof(T).FullName!);
    }

    /// <inheritdoc />
    public CallToolResult SerializeToCallToolResult(IMcpServerCallToolContext context,
        string sourceGeneratedJsonTypeName, string sourceGeneratedJsonTypeFullName) => ResultFactory(Result,
        McpToolJsonTypeInfoNotFoundException.EnsureTypeInfo<T>(context, sourceGeneratedJsonTypeName, sourceGeneratedJsonTypeFullName));
}

/// <summary>
/// 用于将结果序列化为 <see cref="CallToolResult"/> 实例的接口。
/// </summary>
public interface ICallToolResultJsonSerializer
{
    /// <summary>
    /// 将结果序列化为 <see cref="CallToolResult"/> 实例。
    /// </summary>
    /// <param name="jsonSerializerContext">用于序列化结果的 JSON 序列化上下文。</param>
    /// <returns>序列化后的 <see cref="CallToolResult"/> 实例。</returns>
    CallToolResult SerializeToCallToolResult(JsonSerializerContext jsonSerializerContext);

    /// <summary>
    /// 将结果序列化为 <see cref="CallToolResult"/> 实例。
    /// </summary>
    /// <param name="context">调用工具的上下文。</param>
    /// <param name="sourceGeneratedJsonTypeName">由源生成器提供的要被反序列化的类型名称。</param>
    /// <param name="sourceGeneratedJsonTypeFullName">由源生成器提供的要被反序列化的类型完整名称。</param>
    /// <returns>序列化后的 <see cref="CallToolResult"/> 实例。</returns>
    CallToolResult SerializeToCallToolResult(IMcpServerCallToolContext context,
        string sourceGeneratedJsonTypeName, string sourceGeneratedJsonTypeFullName);
}
