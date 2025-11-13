using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.Protocol;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// MCP 服务返回的工具调用结果，包含一个可以被延迟序列化的结果。
/// </summary>
/// <param name="result">要包含的结果。</param>
/// <typeparam name="T">结果的类型。</typeparam>
public sealed class CallToolResult<T>(T result) : CallToolResult, ICallToolResultJsonSerializer
{
    /// <summary>
    /// 获取包含的结果。
    /// </summary>
    [JsonIgnore]
    public T Result { get; } = result;

    /// <summary>
    /// 获取一个用了延迟序列化结果的工厂方法。
    /// </summary>
    [JsonIgnore]
    public required Func<T, JsonTypeInfo<T>, CallToolResult> ResultFactory { get; init; }

    /// <inheritdoc />
    public CallToolResult SerializeToCallToolResult(JsonSerializerContext jsonSerializerContext)
    {
        return ResultFactory(Result, (JsonTypeInfo<T>)jsonSerializerContext.GetTypeInfo(typeof(T))!);
    }
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
}
