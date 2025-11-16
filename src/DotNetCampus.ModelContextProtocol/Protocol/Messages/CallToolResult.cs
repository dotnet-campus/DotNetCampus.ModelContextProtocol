using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 工具调用结果<br/>
/// The server's response to a tool call.
/// </summary>
public record CallToolResult
{
    /// <summary>
    /// 表示工具调用非结构化结果的内容对象列表。<br/>
    /// A list of content objects that represent the unstructured result of the tool call.
    /// </summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<ContentBlock> Content { get; init; } = [];

    /// <summary>
    /// 可选的 JSON 对象，表示工具调用的结构化结果。<br/>
    /// An optional JSON object that represents the structured result of the tool call.
    /// </summary>
    [JsonPropertyName("structuredContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? StructuredContent { get; init; }

    /// <summary>
    /// 工具调用是否以错误结束。<br/>
    /// 如果未设置，则假定为 false（调用成功）。<br/>
    /// 源自工具的任何错误都应该在结果对象内报告，并将 isError 设置为 true，
    /// 而不是作为 MCP 协议级别的错误响应。<br/>
    /// 否则，LLM 将无法看到发生了错误并进行自我纠正。<br/>
    /// 但是，在查找工具时出现的任何错误、表示服务器不支持工具调用的错误或任何其他异常情况，
    /// 都应作为 MCP 错误响应报告。<br/>
    /// Whether the tool call ended in an error.<br/>
    /// If not set, this is assumed to be false (the call was successful).<br/>
    /// Any errors that originate from the tool SHOULD be reported inside the result object,
    /// with isError set to true, _not_ as an MCP protocol-level error response.
    /// Otherwise, the LLM would not be able to see that an error occurred and self-correct.<br/>
    /// However, any errors in _finding_ the tool, an error indicating that the server does not
    /// support tool calls, or any other exceptional conditions, should be reported as an MCP error response.
    /// </summary>
    [JsonPropertyName("isError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsError { get; init; }

    /// <summary>
    /// 返回表示当前实例的字符串。
    /// </summary>
    /// <returns>表示当前实例的字符串。</returns>
    public override string ToString()
    {
        return Content switch
        {
            [] => "",
            [TextContentBlock { Text: var text }] => text,
            _ => $"CallToolResult with {Content.Count} content blocks.",
        };
    }

    /// <summary>
    /// 使用指定的 JSON 序列化上下文将当前实例序列化为结构化的 <see cref="CallToolResult"/> 实例。
    /// 当然，如果当前实例不是结构化实例，则会原样返回当前实例。
    /// </summary>
    /// <param name="jsonSerializerContext">用于序列化的 JSON 序列化上下文。</param>
    /// <returns>结构化的 <see cref="CallToolResult"/> 实例，或者当前实例本身（如果它不是结构化实例）。</returns>
    public CallToolResult Structure(JsonSerializerContext jsonSerializerContext) => this switch
    {
        ICallToolResultJsonSerializer s => s.SerializeToCallToolResult(jsonSerializerContext),
        _ => this,
    };

    /// <summary>
    /// 隐式将字符串转换为一个表示成功的 <see cref="CallToolResult"/> 实例。
    /// </summary>
    /// <param name="textContent">要转换的字符串。</param>
    /// <returns>表示成功的 <see cref="CallToolResult"/> 实例。</returns>
    public static implicit operator CallToolResult(string textContent)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = textContent }],
        };
    }

    /// <summary>
    /// 创建一个表示错误的 <see cref="CallToolResult"/> 实例。
    /// </summary>
    /// <param name="errorMessage">错误消息。</param>
    /// <returns>表示错误的 <see cref="CallToolResult"/> 实例。</returns>
    public static CallToolResult FromError(string errorMessage)
    {
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = errorMessage }],
        };
    }

    /// <summary>
    /// 创建一个表示成功的，包含指定文本内容的 <see cref="CallToolResult"/> 实例。
    /// </summary>
    /// <param name="textContent">要包含的文本内容。</param>
    /// <returns>表示成功的 <see cref="CallToolResult"/> 实例。</returns>
    public static CallToolResult FromResult(string textContent)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = textContent }],
        };
    }

    /// <summary>
    /// 直接返回 <paramref name="result"/> 实例本身。本方法存在的唯一作用，是让源生成器生成的代码能具有完全统一的调用形式。
    /// </summary>
    /// <param name="result">要返回的结果实例。</param>
    /// <returns>传入的结果实例本身。</returns>
    public static CallToolResult FromResult(CallToolResult result)
    {
        return result;
    }

    /// <summary>
    /// 直接返回 <paramref name="result"/> 实例本身。本方法存在的唯一作用，是让源生成器生成的代码能具有完全统一的调用形式。
    /// </summary>
    /// <param name="result">要返回的结果实例。</param>
    /// <typeparam name="TResult">结果的类型。</typeparam>
    /// <returns>传入的结果实例本身。</returns>
    public static CallToolResult<TResult> FromResult<TResult>(CallToolResult<TResult> result)
    {
        return result;
    }

    /// <summary>
    /// 创建一个表示成功的，包含指定结果的 <see cref="CallToolResult{TResult}"/> 实例。
    /// </summary>
    /// <param name="result">要包含的结果。</param>
    /// <typeparam name="TResult">结果的类型。</typeparam>
    /// <returns>一个可以被序列化成 <see cref="CallToolResult"/> 的延迟实例。</returns>
    public static CallToolResult<TResult> FromResult<TResult>(TResult result)
    {
        return new CallToolResult<TResult>(result)
        {
            ResultFactory = (r, t) =>
            {
                var json = JsonSerializer.SerializeToElement(r, t);
                return new CallToolResult
                {
                    Content =
                    [
                        new TextContentBlock
                        {
                            Text = json.ToString(),
                        },
                    ],
                    StructuredContent = json,
                };
            },
        };
    }
}
