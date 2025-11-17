using System.Text.Json;

namespace DotNetCampus.ModelContextProtocol.Core;

/// <summary>
/// 表示一个空的结果对象。
/// </summary>
public readonly record struct EmptyResult
{
    /// <summary>
    /// 获取表示空对象 {} 的 JsonElement。
    /// </summary>
    public static JsonElement JsonElement { get; } = JsonDocument.Parse("{}").RootElement;
}
