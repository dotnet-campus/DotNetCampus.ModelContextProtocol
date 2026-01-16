using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 读取资源的结果<br/>
/// The server's response to a resources/read request from the client.
/// </summary>
public record ReadResourceResult : Result
{
    /// <summary>
    /// 资源内容列表<br/>
    /// The contents of the resource or resources that were read.
    /// </summary>
    [JsonPropertyName("contents")]
    public IReadOnlyList<ResourceContents> Contents { get; set; } = [];

    /// <summary>
    /// 创建包含指定资源内容的 <see cref="ReadResourceResult"/> 实例。
    /// </summary>
    /// <param name="resourceContents">要包含的资源内容。</param>
    /// <returns><see cref="ReadResourceResult"/> 实例。</returns>
    public static ReadResourceResult FromResult(ResourceContents resourceContents)
    {
        return new ReadResourceResult
        {
            Contents =
            [
                resourceContents,
            ],
        };
    }

    /// <summary>
    /// 直接返回 <paramref name="result"/> 实例本身。本方法存在的唯一作用，是让源生成器生成的代码能具有完全统一的调用形式。
    /// </summary>
    /// <param name="result">要返回的结果实例。</param>
    /// <returns>传入的结果实例本身。</returns>
    public static ReadResourceResult FromResult(ReadResourceResult result)
    {
        return result;
    }

    /// <summary>
    /// 空的资源读取结果（用于 void 返回值）。
    /// </summary>
    public static ReadResourceResult Empty => new();
}
