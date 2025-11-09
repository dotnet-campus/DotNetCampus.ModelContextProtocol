using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Messages;

/// <summary>
/// JSON-RPC 2.0 消息基类
/// </summary>
public abstract class JsonRpcMessage
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
public class JsonRpcRequest : JsonRpcMessage
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
public class JsonRpcResponse : JsonRpcMessage
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
public class JsonRpcNotification : JsonRpcMessage
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
public class JsonRpcError
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
