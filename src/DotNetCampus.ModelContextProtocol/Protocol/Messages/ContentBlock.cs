using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 工具调用结果的内容块。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContentBlock), typeDiscriminator: "text")]
[JsonDerivedType(typeof(ImageContentBlock), typeDiscriminator: "image")]
[JsonDerivedType(typeof(AudioContentBlock), typeDiscriminator: "audio")]
[JsonDerivedType(typeof(ResourceLinkContentBlock), typeDiscriminator: "resource_link")]
[JsonDerivedType(typeof(EmbeddedResourceContentBlock), typeDiscriminator: "resource")]
public abstract record ContentBlock
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
public sealed record TextContentBlock : ContentBlock
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
public sealed record ImageContentBlock : ContentBlock
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
public sealed record AudioContentBlock : ContentBlock
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
public sealed record ResourceLinkContentBlock : ContentBlock
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
public sealed record EmbeddedResourceContentBlock : ContentBlock
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
public abstract record ResourceContents
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
public sealed record TextResourceContents : ResourceContents
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
public sealed record BlobResourceContents : ResourceContents
{
    /// <summary>
    /// Base64 编码的二进制数据。
    /// </summary>
    [JsonPropertyName("blob")]
    public required string Blob { get; init; }
}
