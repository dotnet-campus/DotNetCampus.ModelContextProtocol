using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 具有 name（标识符）和 title（显示名称）属性的元数据的基本接口。<br/>
/// Base interface for metadata with name (identifier) and title (display name) properties.
/// </summary>
/// <remarks>
/// 对应官方 MCP Schema 中的 @internal interface BaseMetadata。<br/>
/// 在官方 TypeScript schema 中，这是一个内部接口，用于确保类型一致性。<br/>
/// Corresponds to @internal interface BaseMetadata in the official MCP Schema.<br/>
/// In the official TypeScript schema, this is an internal interface used to ensure type consistency.
/// </remarks>
public interface IBaseMetadata
{
    /// <summary>
    /// 用于编程或逻辑使用，但在过去的规范中或作为后备用于显示名称（如果 title 不存在）。<br/>
    /// Intended for programmatic or logical use, but used as a display name in past specs
    /// or fallback (if title isn't present).
    /// </summary>
    [JsonPropertyName("name")]
    string Name { get; }

    /// <summary>
    /// 用于 UI 和最终用户上下文 — 优化为可读并易于理解，即使对不熟悉特定领域术语的人也是如此。<br/>
    /// 如果未提供，应使用 name 作为显示名称（针对 Tool，如果存在 annotations.title，应优先使用它而不是 name）。<br/>
    /// Intended for UI and end-user contexts — optimized to be human-readable
    /// and easily understood, even by those unfamiliar with domain-specific terminology.<br/>
    /// If not provided, the name should be used for display
    /// (except for Tool, where annotations.title should be given precedence over using name, if present).
    /// </summary>
    [JsonPropertyName("title")]
    string? Title { get; }
}
