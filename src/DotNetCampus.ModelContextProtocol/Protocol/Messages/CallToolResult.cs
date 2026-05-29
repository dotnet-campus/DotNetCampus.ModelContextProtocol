using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 工具调用结果<br/>
/// The server's response to a tool call.
/// </summary>
public record CallToolResult : Result
{
    /// <summary>
    /// 表示空的工具调用结果。<br/>
    /// An empty tool call result.
    /// </summary>
    public static CallToolResult Empty { get; } = new CallToolResult
    {
        IsError = false,
        Content = [],
    };

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
    /// 如果工具调用过程中发生了异常，则此属性包含该异常对象。<br/>
    /// 该属性不会被序列化到 MCP 协议中，仅供服务器端代码使用。<br/>
    /// If an exception occurred during the tool call, this property contains the exception object.<br/>
    /// This property is not serialized into the MCP protocol and is only for server-side code use.
    /// </summary>
    [JsonIgnore]
    public Exception? RawException { get; init; }

    /// <summary>
    /// 返回表示当前实例的字符串。
    /// </summary>
    /// <returns>表示当前实例的字符串。</returns>
    public override string ToString()
    {
        if (StructuredContent is { } structuredContent)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
            structuredContent.WriteTo(writer);
            writer.Flush();
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        if (Content.Count == 0)
        {
            return IsError == true ? "{\"error\":\"Call tool failed.\"}" : string.Empty;
        }

        if (Content is [TextContentBlock { Text: var text }])
        {
            return text;
        }

        return string.Join("\n", Content.Select(x => x.ToString()));
    }

    /// <summary>
    /// 隐式将字符串转换为一个表示成功的 <see cref="CallToolResult"/> 实例。
    /// </summary>
    /// <param name="textContent">要转换的字符串。</param>
    /// <returns>表示成功的 <see cref="CallToolResult"/> 实例。</returns>
    public static implicit operator CallToolResult(string? textContent)
    {
        return new CallToolResult
        {
            IsError = false,
            Content = textContent is null
                ? []
                : [new TextContentBlock { Text = textContent }],
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
    /// 从异常创建一个表示错误的 <see cref="CallToolResult"/> 实例。
    /// </summary>
    /// <param name="exception">异常。</param>
    /// <param name="errorMessage">如果指定，则使用此错误消息代替异常消息。</param>
    /// <returns>表示错误的 <see cref="CallToolResult"/> 实例。</returns>
    public static CallToolResult FromException(Exception exception, string? errorMessage = null)
    {
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = errorMessage ?? exception.Message }],
        };
    }

    /// <summary>
    /// 创建一个表示成功的，包含指定文本内容的 <see cref="CallToolResult"/> 实例。
    /// </summary>
    /// <param name="textContent">要包含的文本内容。</param>
    /// <returns>表示成功的 <see cref="CallToolResult"/> 实例。</returns>
    public static CallToolResult FromResult(string? textContent)
    {
        return new CallToolResult
        {
            IsError = false,
            Content = textContent is null
                ? [new TextContentBlock { Text = "" }]
                : [new TextContentBlock { Text = textContent }],
        };
    }

    /// <summary>
    /// 直接返回 <paramref name="result"/> 实例本身。
    /// </summary>
    /// <param name="result">要返回的结果实例。</param>
    /// <returns>传入的结果实例本身。</returns>
    public static CallToolResult FromResult(CallToolResult result)
    {
        return result;
    }

    /// <summary>
    /// 使用指定的 <see cref="JsonTypeInfo{T}"/> 立即序列化结果，创建同时包含
    /// Content（JSON 文本）和 StructuredContent（JSON 对象）的 <see cref="CallToolResult"/>。
    /// </summary>
    /// <param name="result">要序列化的结果值。</param>
    /// <param name="jsonTypeInfo">用于序列化结果的 <see cref="JsonTypeInfo{T}"/>。</param>
    /// <typeparam name="TResult">结果的类型。</typeparam>
    /// <returns>序列化后的 <see cref="CallToolResult"/> 实例。</returns>
    public static CallToolResult FromResultStructured<TResult>(TResult? result, JsonTypeInfo<TResult> jsonTypeInfo)
    {
        if (result is null)
        {
            // 对于结构化返回值，null 是 MCP 协议明确不支持的。
            //   对于编译时可判定的情况，我们已经让开发者通过设置 McpServerToolAttribute.Structured = false 来避免进行结构化；
            //   但编译时尽力了，运行时得到了 null，已经无法生成符合要求的结构化返回值了。
            //   无论我们返回什么，都会导致 MCP 客户端校验不通过；不如实际上就不要返回任何除协议之外的内容了。
            // 开发者可通过 MCP 客户端的报错得知这个 bug。
            return new CallToolResult
            {
                IsError = false,
                Content = [],
            };
        }

        if (result is CallToolResult r)
        {
            return r;
        }

        var json = JsonSerializer.SerializeToElement(result, jsonTypeInfo);
        return new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = json.ToString() }],
            StructuredContent = json,
        };
    }

    /// <summary>
    /// 使用指定的 <see cref="JsonTypeInfo{T}"/> 立即序列化结果，创建仅包含
    /// Content（JSON 文本）的 <see cref="CallToolResult"/>，不包含 StructuredContent。
    /// </summary>
    /// <param name="result">要序列化的结果值。</param>
    /// <param name="typeInfo">用于序列化结果的 <see cref="JsonTypeInfo{T}"/>。</param>
    /// <typeparam name="TResult">结果的类型。</typeparam>
    /// <returns>序列化后的 <see cref="CallToolResult"/> 实例。</returns>
    public static CallToolResult FromResultUnstructured<TResult>(TResult? result, JsonTypeInfo<TResult> typeInfo) => result switch
    {
        // 对于非结构化返回值，我们可以在 MCP 协议层兜底，返回空字符串避免一部分 MCP 客户端的失败。
        null => new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = "" }],
        },
        CallToolResult r => r,
        string s => new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = s }],
        },
        _ => new CallToolResult
        {
            IsError = false,
            Content =
            [
                new TextContentBlock
                {
                    Text = JsonSerializer.SerializeToElement(result, typeInfo).ToString(),
                },
            ],
        },
    };

    /// <summary>
    /// 将集合转换为 <see cref="CallToolResult"/>，每个元素通过指定的文本提取函数转换为独立的 TextContentBlock。
    /// </summary>
    /// <typeparam name="TItem">集合元素的类型。</typeparam>
    /// <param name="items">集合。</param>
    /// <param name="textContentGenerator">从元素中生成可填入 <see cref="TextContentBlock"/> 文本的函数。</param>
    /// <returns>包含多个 TextContentBlock 的 <see cref="CallToolResult"/> 实例。</returns>
    public static CallToolResult FromCollection<TItem>(IEnumerable<TItem>? items, Func<TItem, string> textContentGenerator) => items switch
    {
        // 对于集合，即便我们不返回空内容 `[]`，也会因为集合本身没有任何项而导致生成空内容 `[]`；
        // 所以虽然 `[]` 可能无法通过一部分客户端的校验，但也只能这样返回了。
        null => new CallToolResult
        {
            IsError = false,
            Content = [],
        },
        _ => new CallToolResult
        {
            IsError = false,
            Content = [.. items.Select(item => new TextContentBlock { Text = textContentGenerator(item) })],
        },
    };
}
