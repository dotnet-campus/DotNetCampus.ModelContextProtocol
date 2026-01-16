using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 表示客户端用于获取服务器提供的资源的 <see cref="RequestMethods.ResourcesRead"/> 请求的参数。<br/>
/// The parameters used with a <see cref="RequestMethods.ResourcesRead"/> request from a client to get a resource provided by a server.
/// </summary>
public sealed record ReadResourceRequestParams : RequestParams
{
    /// <summary>
    /// 要读取的资源的 URI。URI 可以使用任何协议；由服务器决定如何解释它。<br/>
    /// The URI of the resource to read. The URI can use any protocol; it is up to the server how to interpret it.
    /// </summary>
    [JsonPropertyName("uri")]
    [StringSyntax(StringSyntaxAttribute.Uri)]
    public required string Uri { get; set; }
}
