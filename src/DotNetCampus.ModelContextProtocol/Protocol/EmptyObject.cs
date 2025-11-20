using System.Text.Json;

namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// 表示一个空的对象，MCP 协议传输时作为 Json 空对象 {} 使用。
/// </summary>
public readonly record struct EmptyObject
{
    /// <summary>
    /// 获取表示空对象 {} 的 JsonElement。
    /// </summary>
    public static JsonElement JsonElement { get; } = JsonDocument.Parse("{}").RootElement;
}
