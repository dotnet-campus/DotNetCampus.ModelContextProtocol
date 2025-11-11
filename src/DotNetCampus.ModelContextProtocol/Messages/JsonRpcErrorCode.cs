namespace DotNetCampus.ModelContextProtocol.Messages;

/// <summary>
/// JSON-RPC 2.0 错误码定义
/// 参考: https://www.jsonrpc.org/specification#error_object
/// </summary>
public enum JsonRpcErrorCode
{
    /// <summary>
    /// 解析错误 - 服务端接收到无效的 JSON
    /// </summary>
    ParseError = -32700,

    /// <summary>
    /// 无效请求 - 发送的 JSON 不是一个有效的请求对象
    /// </summary>
    InvalidRequest = -32600,

    /// <summary>
    /// 方法不存在 - 请求的方法不存在或不可用
    /// </summary>
    MethodNotFound = -32601,

    /// <summary>
    /// 无效参数 - 无效的方法参数
    /// </summary>
    InvalidParams = -32602,

    /// <summary>
    /// 内部错误 - JSON-RPC 内部错误
    /// </summary>
    InternalError = -32603,

    // -32000 到 -32099 为服务器错误保留
    // MCP 协议使用 -32001 作为通用错误码

    /// <summary>
    /// MCP 协议通用错误
    /// </summary>
    McpError = -32001,
}
