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
    public IList<ResourceContents> Contents { get; set; } = [];
}
