using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// MCP 服务返回的工具调用结果。
/// </summary>
public sealed class CallToolResult
{
    /// <summary>
    /// 获取或设置 MCP 工具响应工具调用所返回的内容。
    /// </summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<ContentBlock> Content { get; init; } = [];

    /// <summary>
    /// 获取或设置一个值，该值指示工具调用是否失败。
    /// </summary>
    [JsonPropertyName("isError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsError { get; set; }

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
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContentBlock), typeDiscriminator: "text")]
public abstract class ContentBlock;

public sealed class TextContentBlock : ContentBlock
{
    /// <summary>
    /// Gets or sets the text content of the message.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}
