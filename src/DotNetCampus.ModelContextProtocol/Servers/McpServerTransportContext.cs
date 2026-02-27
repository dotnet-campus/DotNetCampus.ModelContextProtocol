using System.Collections.Specialized;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 服务器传输层上下文。在业务代码中可通过此类型的子类获取本次请求处理中传输层的一些信息。
/// </summary>
public abstract record McpServerTransportContext
{
};

/// <summary>
/// 使用 Streamable HTTP / SSE 作为传输层时，可通过此类型获取传输层的一些信息。
/// </summary>
public record HttpServerTransportContext
{
    /// <summary>
    /// 本次请求对应的会话 ID。<see langword="null"/> 表示客户端未提供会话 ID。
    /// </summary>
    public required string? SessionId { get; init; }

    /// <summary>
    /// 本次请求的 HTTP 头集合。
    /// </summary>
    public required NameValueCollection Headers { get; init; }
};

/// <summary>
/// 使用 stdio 作为传输层时，可通过此类型获取传输层的一些信息。
/// </summary>
public record StdioServerTransportContext();
