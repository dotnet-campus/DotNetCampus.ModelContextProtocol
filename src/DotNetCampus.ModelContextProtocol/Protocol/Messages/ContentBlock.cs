using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 内容块<br/>
/// Content block that can be part of a message or result
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContentBlock), typeDiscriminator: "text")]
[JsonDerivedType(typeof(ImageContentBlock), typeDiscriminator: "image")]
[JsonDerivedType(typeof(AudioContentBlock), typeDiscriminator: "audio")]
[JsonDerivedType(typeof(ResourceLinkContentBlock), typeDiscriminator: "resource_link")]
[JsonDerivedType(typeof(EmbeddedResourceContentBlock), typeDiscriminator: "resource")]
[JsonDerivedType(typeof(ToolUseContent), typeDiscriminator: "toolUse")]
[JsonDerivedType(typeof(ToolResultContent), typeDiscriminator: "toolResult")]
public abstract record ContentBlock
{
    /// <summary>
    /// 可选的客户端注解。<br/>
    /// Optional annotations for the client.
    /// </summary>
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Annotations? Annotations { get; init; }

    /// <summary>
    /// 元数据字段<br/>
    /// See <a href="https://modelcontextprotocol.io/specification/2025-11-25/basic/index#meta">
    /// General fields: _meta</a> for notes on _meta usage.
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Meta { get; init; }
}

/// <summary>
/// 文本内容块<br/>
/// Text provided to or from an LLM.
/// </summary>
public sealed record TextContentBlock : ContentBlock
{
    /// <summary>
    /// 文本内容<br/>
    /// The text content of the message.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
/// 图像内容块<br/>
/// An image provided to or from an LLM.
/// </summary>
public sealed record ImageContentBlock : ContentBlock
{
    /// <summary>
    /// Base64 编码的图像数据<br/>
    /// The base64-encoded image data.
    /// </summary>
    [JsonPropertyName("data")]
    public required string Data { get; init; }

    /// <summary>
    /// 图像的 MIME 类型。不同的提供商可能支持不同的图像类型。<br/>
    /// The MIME type of the image. Different providers may support different image types.
    /// </summary>
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }
}

/// <summary>
/// 音频内容块<br/>
/// Audio provided to or from an LLM.
/// </summary>
public sealed record AudioContentBlock : ContentBlock
{
    /// <summary>
    /// Base64 编码的音频数据<br/>
    /// The base64-encoded audio data.
    /// </summary>
    [JsonPropertyName("data")]
    public required string Data { get; init; }

    /// <summary>
    /// 音频的 MIME 类型。不同的提供商可能支持不同的音频类型。<br/>
    /// The MIME type of the audio. Different providers may support different audio types.
    /// </summary>
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }
}

/// <summary>
/// 资源链接内容块<br/>
/// A resource that the server is capable of reading, included in a prompt or tool call result.<br/>
/// Note: resource links returned by tools are not guaranteed to appear in
/// the results of resources/list requests.
/// </summary>
public sealed record ResourceLinkContentBlock : ContentBlock
{
    /// <summary>
    /// 资源的 URI<br/>
    /// The URI of this resource.
    /// </summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    /// <summary>
    /// 用于编程或逻辑使用，但在过去的规范中或作为后备用于显示名称（如果 title 不存在）。<br/>
    /// Intended for programmatic or logical use, but used as a display name in past specs
    /// or fallback (if title isn't present).
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 用于 UI 和最终用户上下文 — 优化为可读并易于理解，即使对不熟悉特定领域术语的人也是如此。<br/>
    /// 如果未提供，应使用 name 作为显示名称。<br/>
    /// Intended for UI and end-user contexts — optimized to be human-readable
    /// and easily understood, even by those unfamiliar with domain-specific terminology.<br/>
    /// If not provided, the name should be used for display.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// 该资源的作用描述。<br/>
    /// 客户端可以使用这些信息来改善 LLM 对可用资源的理解。<br/>
    /// 可以将其视为给模型的"提示"。<br/>
    /// A description of what this resource represents.<br/>
    /// This can be used by clients to improve the LLM's understanding of available resources.
    /// It can be thought of like a "hint" to the model.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// 资源的 MIME 类型（如果已知）。<br/>
    /// The MIME type of this resource, if known.
    /// </summary>
    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; init; }

    /// <summary>
    /// 原始资源内容的大小（以字节为单位）（即，在 base64 编码或任何分词之前），如果已知。<br/>
    /// 客户端可以使用它来显示文件大小并估计上下文窗口使用情况。<br/>
    /// The size of the raw resource content, in bytes (i.e., before base64 encoding or any tokenization),
    /// if known.<br/>
    /// This can be used by Hosts to display file sizes and estimate context window usage.
    /// </summary>
    [JsonPropertyName("size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Size { get; init; }
}

/// <summary>
/// 嵌入资源内容块<br/>
/// The contents of a resource, embedded into a prompt or tool call result.<br/>
/// It is up to the client how best to render embedded resources for the benefit of the LLM and/or the user.
/// </summary>
public sealed record EmbeddedResourceContentBlock : ContentBlock
{
    /// <summary>
    /// 嵌入的资源内容<br/>
    /// The embedded resource content
    /// </summary>
    [JsonPropertyName("resource")]
    public required ResourceContents Resource { get; init; }
}

/// <summary>
/// 资源内容基类<br/>
/// The contents of a specific resource or sub-resource.
/// </summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(TextResourceContents), typeDiscriminator: "text")]
[JsonDerivedType(typeof(BlobResourceContents), typeDiscriminator: "blob")]
public abstract record ResourceContents
{
    /// <summary>
    /// 资源的 URI<br/>
    /// The URI of this resource.
    /// </summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    /// <summary>
    /// 资源的 MIME 类型（如果已知）。<br/>
    /// The MIME type of this resource, if known.
    /// </summary>
    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; init; }

    /// <summary>
    /// 元数据字段<br/>
    /// See <a href="https://modelcontextprotocol.io/specification/2025-11-25/basic/index#meta">
    /// General fields: _meta</a> for notes on _meta usage.
    /// </summary>
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Meta { get; init; }
}

/// <summary>
/// 文本资源内容<br/>
/// Text resource contents
/// </summary>
public sealed record TextResourceContents : ResourceContents
{
    /// <summary>
    /// 文本内容。仅当项目实际上可以表示为文本（非二进制数据）时才必须设置此项。<br/>
    /// The text of the item. This must only be set if the item can actually be represented
    /// as text (not binary data).
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
/// 二进制资源内容（Base64 编码）<br/>
/// Binary resource contents (Base64 encoded)
/// </summary>
public sealed record BlobResourceContents : ResourceContents
{
    /// <summary>
    /// 表示项目二进制数据的 base64 编码字符串。<br/>
    /// A base64-encoded string representing the binary data of the item.
    /// </summary>
    [JsonPropertyName("blob")]
    public required string Blob { get; init; }
}

/// <summary>
/// 工具使用内容块<br/>
/// Tool use content block
/// </summary>
public sealed record ToolUseContent : ContentBlock
{
    /// <summary>
    /// 工具使用的唯一标识符。<br/>
    /// Unique identifier for this tool use.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// 要调用的工具名称。<br/>
    /// The name of the tool to call.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 传递给工具的参数。<br/>
    /// Arguments to pass to the tool.
    /// </summary>
    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Input { get; init; }
}

/// <summary>
/// 工具结果内容块<br/>
/// Tool result content block
/// </summary>
public sealed record ToolResultContent : ContentBlock
{
    /// <summary>
    /// 对应的工具使用标识符。<br/>
    /// The identifier of the corresponding tool use.
    /// </summary>
    [JsonPropertyName("toolUseId")]
    public required string ToolUseId { get; init; }

    /// <summary>
    /// 非结构化的工具执行结果内容。<br/>
    /// Unstructured tool execution result content.
    /// </summary>
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ContentBlock>? Content { get; init; }

    /// <summary>
    /// 结构化的工具执行结果。<br/>
    /// Structured tool execution result.
    /// </summary>
    [JsonPropertyName("structuredContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? StructuredContent { get; init; }

    /// <summary>
    /// 指示工具执行是否遇到错误。<br/>
    /// Indicates whether the tool execution encountered an error.
    /// </summary>
    [JsonPropertyName("isError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsError { get; init; }
}
