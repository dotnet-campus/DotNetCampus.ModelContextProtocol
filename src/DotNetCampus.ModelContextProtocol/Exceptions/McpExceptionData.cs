using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// 当 MCP 服务器响应请求时，如果发生异常，则使用此类型来向客户端传递异常信息。
/// </summary>
public record McpExceptionData
{
    /// <summary>
    /// 异常类型的完全限定名称。
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// 异常消息。
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// 异常的堆栈跟踪信息。
    /// </summary>
    [JsonPropertyName("stackTrace")]
    public required string StackTrace { get; init; }

    /// <summary>
    /// 将当前实例序列化为 <see cref="JsonElement"/>。
    /// </summary>
    /// <returns>表示当前实例的 <see cref="JsonElement"/>。</returns>
    public JsonElement ToJsonElement()
    {
        return JsonSerializer.SerializeToElement(this, McpServerResponseJsonContext.Default.McpExceptionData);
    }

    /// <summary>
    /// 从异常对象创建 <see cref="McpExceptionData"/> 实例。
    /// </summary>
    /// <param name="exception">异常对象。</param>
    /// <typeparam name="TException">异常类型。</typeparam>
    /// <returns>对应的 <see cref="McpExceptionData"/> 实例。</returns>
    public static McpExceptionData From<TException>(TException exception)
        where TException : Exception => new()
    {
        Type = typeof(TException).FullName!,
        Message = exception.Message,
        StackTrace = exception.StackTrace ?? string.Empty,
    };
}
