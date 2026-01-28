using System.Diagnostics.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Transports.TouchSocket;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 扩展 <see cref="McpServerBuilder"/> 以支持 TouchSocket.Http 相关功能。
/// </summary>
public static class McpServerBuilderTouchSocketHttpExtensions
{
    /// <param name="builder">用于链式调用的 MCP 服务器生成器。</param>
    extension(McpServerBuilder builder)
    {
        /// <summary>
        /// 允许此 MCP 服务器通过 TouchSocket.Http 提供服务。
        /// </summary>
        /// <param name="endPoint">
        /// MCP 服务器将监听的路由端点，例如指定为 mcp 时，完整的 URL 为 http://localhost:{port}/mcp。<br/>
        /// 如果希望监听根路径，请指定为空字符串 ""。
        /// </param>
        /// <param name="port">MCP 服务器将监听 http://localhost:{port} 上的请求。</param>
        /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
        public McpServerBuilder WithTouchSocketHttp(int port, [StringSyntax("Route")] string? endPoint = null)
        {
            builder.WithTouchSocketHttp(new TouchSocketHttpServerTransportOptions
            {
                Listen = [$"localhost:{port}", $"127.0.0.1:{port}", $"[::1]:{port}"],
                EndPoint = endPoint,
            });
            return builder;
        }

        /// <summary>
        /// 允许此 MCP 服务器通过 TouchSocket.Http 提供服务。
        /// </summary>
        /// <param name="listen">
        /// 指定监听的主机和端口列表。
        /// <code>
        /// // 监听格式："IPv4:端口", "IPv6:端口"
        /// // 只能使用IP地址和端口号进行监听，不能使用域名。
        /// [$"127.0.0.1:{Port}", $"[::1]:{Port}"]
        /// [$"0.0.0.0:{Port}", $"[::]:{Port}"]
        /// // 可监听 1 个或多个地址，也可以有各自不同的端口号。
        /// </code>
        /// </param>
        /// <param name="endPoint">
        /// MCP 服务器将监听的路由端点，例如指定为 mcp 时，完整的 URL 为 http://localhost:{port}/mcp。
        /// </param>
        /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
        public McpServerBuilder WithTouchSocketHttp(IReadOnlyList<string> listen, [StringSyntax("Route")] string? endPoint = null)
        {
            builder.WithTouchSocketHttp(new TouchSocketHttpServerTransportOptions
            {
                Listen = listen,
                EndPoint = endPoint,
            });
            return builder;
        }

        /// <summary>
        /// 允许此 MCP 服务器通过 TouchSocket.Http 提供服务。
        /// </summary>
        /// <param name="options">TouchSocket.Http 服务选项。</param>
        /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
        public McpServerBuilder WithTouchSocketHttp(TouchSocketHttpServerTransportOptions options)
        {
            builder.WithTransport(m => new TouchSocketHttpServerTransport(m, options));
            return builder;
        }
    }
}
