namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// MCP 服务返回的工具调用结果。
/// </summary>
public sealed class CallToolResult
{
    /// <summary>
    /// 获取或设置 MCP 工具响应工具调用所返回的内容。
    /// </summary>
    public IReadOnlyList<ContentBlock> Content { get; init; } = [];

    /// <summary>
    /// 获取或设置一个值，该值指示工具调用是否失败。
    /// </summary>
    public bool? IsError { get; set; }

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

public abstract class ContentBlock
{
    public string Type { get; set; } = "";
}

public sealed class TextContentBlock : ContentBlock
{
    public TextContentBlock()
    {
        Type = "text";
    }

    /// <summary>
    /// Gets or sets the text content of the message.
    /// </summary>
    public required string Text { get; init; }
}
