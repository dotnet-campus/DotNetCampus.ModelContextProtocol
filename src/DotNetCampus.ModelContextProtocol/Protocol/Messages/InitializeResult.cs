using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 初始化响应结果<br/>
/// After receiving an initialize request from the client, the server sends this response.
/// </summary>
public record InitializeResult : Result
{
    /// <summary>
    /// 服务器希望使用的 Model Context Protocol 版本。<br/>
    /// 这可能与客户端请求的版本不匹配。<br/>
    /// 如果客户端无法支持该版本，必须断开连接。<br/>
    /// The version of the Model Context Protocol that the server wants to use.
    /// This may not match the version that the client requested.
    /// If the client cannot support this version, it MUST disconnect.
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public required string ProtocolVersion { get; init; }

    /// <summary>
    /// 服务端能力<br/>
    /// Server capabilities
    /// </summary>
    [JsonPropertyName("capabilities")]
    public required ServerCapabilities Capabilities { get; init; }

    /// <summary>
    /// 服务端信息<br/>
    /// Server information
    /// </summary>
    [JsonPropertyName("serverInfo")]
    public required Implementation ServerInfo { get; init; }

    /// <summary>
    /// 描述如何使用服务器及其功能的说明。<br/>
    /// 客户端可以使用这些信息来改善 LLM 对可用工具、资源等的理解。<br/>
    /// 可以将其视为给模型的“提示”。<br/>
    /// 例如，这些信息可能被添加到系统提示中。<br/>
    /// Instructions describing how to use the server and its features.<br/>
    /// This can be used by clients to improve the LLM's understanding of available tools,
    /// resources, etc. It can be thought of like a "hint" to the model.
    /// For example, this information MAY be added to the system prompt.
    /// </summary>
    [JsonPropertyName("instructions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Instructions { get; init; }
}
