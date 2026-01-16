using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 初始化请求参数<br/>
/// This request is sent from the client to the server when it first connects,
/// asking it to begin initialization.
/// </summary>
public sealed record InitializeRequestParams : RequestParams
{
    /// <summary>
    /// 客户端支持的最新 Model Context Protocol 版本。<br/>
    /// 客户端也可以决定支持旧版本。<br/>
    /// The latest version of the Model Context Protocol that the client supports.
    /// The client MAY decide to support older versions as well.
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public required string ProtocolVersion { get; init; }

    /// <summary>
    /// 客户端能力<br/>
    /// Client capabilities
    /// </summary>
    [JsonPropertyName("capabilities")]
    public required ClientCapabilities Capabilities { get; init; }

    /// <summary>
    /// 客户端信息<br/>
    /// Client information
    /// </summary>
    [JsonPropertyName("clientInfo")]
    public required Implementation ClientInfo { get; init; }
}
