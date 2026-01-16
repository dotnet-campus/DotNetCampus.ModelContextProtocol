namespace DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

/// <summary>
/// JSON-RPC 2.0 错误码定义<br/>
/// JSON-RPC 2.0 error code definitions<br/>
/// 参考 Reference: https://www.jsonrpc.org/specification#error_object
/// </summary>
public enum JsonRpcErrorCode
{
    /// <summary>
    /// 解析错误 - 服务端接收到无效的 JSON<br/>
    /// Parse error - Invalid JSON was received by the server
    /// </summary>
    ParseError = -32700,

    /// <summary>
    /// 无效请求 - 发送的 JSON 不是一个有效的请求对象<br/>
    /// Invalid Request - The JSON sent is not a valid Request object
    /// </summary>
    InvalidRequest = -32600,

    /// <summary>
    /// 方法不存在 - 请求的方法不存在或不可用<br/>
    /// Method not found - The method does not exist or is not available
    /// </summary>
    MethodNotFound = -32601,

    /// <summary>
    /// 无效参数 - 无效的方法参数<br/>
    /// Invalid params - Invalid method parameter(s)
    /// </summary>
    InvalidParams = -32602,

    /// <summary>
    /// 内部错误 - JSON-RPC 内部错误<br/>
    /// Internal error - Internal JSON-RPC error
    /// </summary>
    InternalError = -32603,

    // -32000 到 -32099 为服务器错误保留
    // -32000 to -32099 are reserved for server errors
    // MCP 协议使用 -32001 作为通用错误码
    // MCP protocol uses -32001 as a general error code

    /// <summary>
    /// MCP 协议通用错误<br/>
    /// General MCP protocol error
    /// </summary>
    McpError = -32001,

    /// <summary>
    /// URL 引出所需错误<br/>
    /// URL elicitation required error
    /// </summary>
    UrlElicitationRequired = -32042,
}
