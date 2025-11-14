using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Messages;

/// <summary>
/// JSON-RPC 2.0 消息基类
/// </summary>
public abstract record JsonRpcMessage
{
    /// <summary>
    /// JSON-RPC 协议版本，必须为 "2.0"
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
}

/// <summary>
/// JSON-RPC 请求
/// </summary>
public record JsonRpcRequest : JsonRpcMessage
{
    /// <summary>
    /// 请求 ID（字符串或数字）
    /// </summary>
    [JsonPropertyName("id")]
    public object? Id { get; set; }

    /// <summary>
    /// 方法名
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    /// <summary>
    /// 参数（可选）
    /// </summary>
    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; set; }
}

/// <summary>
/// JSON-RPC 响应
/// </summary>
public record JsonRpcResponse : JsonRpcMessage
{
    /// <summary>
    /// 请求 ID
    /// </summary>
    [JsonPropertyName("id")]
    public object? Id { get; set; }

    /// <summary>
    /// 成功响应结果
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    /// <summary>
    /// 错误信息（与 result 互斥）
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }
}

/// <summary>
/// JSON-RPC 通知（不需要响应）
/// </summary>
public record JsonRpcNotification : JsonRpcMessage
{
    /// <summary>
    /// 方法名
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    /// <summary>
    /// 参数（可选）
    /// </summary>
    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; set; }
}

/// <summary>
/// JSON-RPC 错误
/// </summary>
public record JsonRpcError
{
    /// <summary>
    /// 错误代码
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    /// <summary>
    /// 额外的错误数据（可选）
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

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

