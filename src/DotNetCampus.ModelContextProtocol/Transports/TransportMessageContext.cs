using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// 传输消息上下文。<br/>
/// 包含 JSON-RPC 消息和传输层特定的上下文信息。<br/>
/// Transport message context.<br/>
/// Contains JSON-RPC message and transport-specific context information.
/// </summary>
/// <param name="Message">JSON-RPC 消息</param>
/// <param name="Context">传输层上下文（可选，用于多对一传输层）</param>
public readonly record struct TransportMessageContext(
    JsonRpcMessage Message,
    ITransportContext Context);
