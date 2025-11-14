using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

/// <summary>
/// JSON-RPC 2.0 消息基类
/// </summary>
public abstract record JsonRpcMessage
{
    /// <summary>
    /// JSON-RPC 协议版本，必须为 "2.0"
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
}
