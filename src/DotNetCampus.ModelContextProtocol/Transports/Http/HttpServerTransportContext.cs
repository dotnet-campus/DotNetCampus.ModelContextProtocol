using System.Collections.Specialized;
using System.Net;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

/// <summary>
/// HTTP 传输层上下文。<br/>
/// 包含 HTTP 请求的会话信息和头部。<br/>
/// HTTP transport context.<br/>
/// Contains session information and headers of the HTTP request.
/// </summary>
public class HttpServerTransportContext : ITransportContext
{
    /// <inheritdoc />
    public required string? SessionId { get; init; }

    /// <summary>
    /// HTTP 请求头<br/>
    /// HTTP request headers
    /// </summary>
    public required NameValueCollection Headers { get; init; }

    /// <summary>
    /// HTTP 监听器上下文（可选，用于直接响应）<br/>
    /// HTTP listener context (optional, for direct response)
    /// </summary>
    internal HttpListenerContext? HttpContext { get; init; }
}
