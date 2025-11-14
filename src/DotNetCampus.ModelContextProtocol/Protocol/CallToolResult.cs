using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// MCP 服务返回的工具调用结果。
/// </summary>
public class CallToolResult
{
    /// <summary>
    /// 获取或设置 MCP 工具响应工具调用所返回的内容。
    /// </summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<ContentBlock> Content { get; init; } = [];

    /// <summary>
    /// 获取或设置一个可选的 JSON 对象，表示工具调用的结构化结果。
    /// </summary>
    [JsonPropertyName("structuredContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? StructuredContent { get; init; }

    /// <summary>
    /// 获取或设置一个值，该值指示工具调用是否失败。
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
    /// 创建一个表示成功的，包含指定结果的 <see cref="CallToolResult{TResult}"/> 实例。
    /// </summary>
    /// <param name="result">要包含的结果。</param>
    /// <typeparam name="TResult">结果的类型。</typeparam>
    /// <returns>一个可以被序列化成 <see cref="CallToolResult"/> 的延迟实例。</returns>
    public static CallToolResult<TResult> FromResult<TResult>(TResult result)
    {
        return new CallToolResult<TResult>(result)
        {
            ResultFactory = (r, t) => JsonSerializer.Serialize(r, t),
        };
    }
}

/// <summary>
/// 工具调用结果的内容块。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContentBlock), typeDiscriminator: "text")]
[JsonDerivedType(typeof(ImageContentBlock), typeDiscriminator: "image")]
[JsonDerivedType(typeof(AudioContentBlock), typeDiscriminator: "audio")]
[JsonDerivedType(typeof(ResourceLinkContentBlock), typeDiscriminator: "resource_link")]
[JsonDerivedType(typeof(EmbeddedResourceContentBlock), typeDiscriminator: "resource")]
public abstract class ContentBlock
{
    /// <summary>
    /// 可选的客户端注解。
    /// </summary>
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Annotations? Annotations { get; init; }

    /// <summary>
    /// 元数据字段。
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Meta { get; init; }
}

/// <summary>
/// 工具调用结果为文本时的内容块。
/// </summary>
public sealed class TextContentBlock : ContentBlock
{
    /// <summary>
    /// 获取或设置文本内容。
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
/// 图像内容块。
/// </summary>
public sealed class ImageContentBlock : ContentBlock
{
    /// <summary>
    /// Base64 编码的图像数据。
    /// </summary>
    [JsonPropertyName("data")]
    public required string Data { get; init; }

    /// <summary>
    /// 图像的 MIME 类型（例如 image/png）。
    /// </summary>
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }
}

/// <summary>
/// 音频内容块。
/// </summary>
public sealed class AudioContentBlock : ContentBlock
{
    /// <summary>
    /// Base64 编码的音频数据。
    /// </summary>
    [JsonPropertyName("data")]
    public required string Data { get; init; }

    /// <summary>
    /// 音频的 MIME 类型（例如 audio/mp3）。
    /// </summary>
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }
}

/// <summary>
/// 资源链接内容块。
/// </summary>
public sealed class ResourceLinkContentBlock : ContentBlock
{
    /// <summary>
    /// 资源的 URI。
    /// </summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    /// <summary>
    /// 资源的名称（标识符）。
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 资源的标题（显示名称）。
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// 资源的描述。
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// 资源的 MIME 类型。
    /// </summary>
    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; init; }

    /// <summary>
    /// 原始资源内容的大小（字节数）。
    /// </summary>
    [JsonPropertyName("size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Size { get; init; }
}

/// <summary>
/// 嵌入资源内容块。
/// </summary>
public sealed class EmbeddedResourceContentBlock : ContentBlock
{
    /// <summary>
    /// 嵌入的资源数据（文本或二进制）。
    /// </summary>
    [JsonPropertyName("resource")]
    public required ResourceContents Resource { get; init; }
}

/// <summary>
/// 资源内容基类。
/// </summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(TextResourceContents), typeDiscriminator: "text")]
[JsonDerivedType(typeof(BlobResourceContents), typeDiscriminator: "blob")]
public abstract class ResourceContents
{
    /// <summary>
    /// 资源的 URI。
    /// </summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    /// <summary>
    /// 资源的 MIME 类型。
    /// </summary>
    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; init; }

    /// <summary>
    /// 元数据字段。
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Meta { get; init; }
}

/// <summary>
/// 文本资源内容。
/// </summary>
public sealed class TextResourceContents : ResourceContents
{
    /// <summary>
    /// 文本内容。
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
/// 二进制资源内容（Base64 编码）。
/// </summary>
public sealed class BlobResourceContents : ResourceContents
{
    /// <summary>
    /// Base64 编码的二进制数据。
    /// </summary>
    [JsonPropertyName("blob")]
    public required string Blob { get; init; }
}
