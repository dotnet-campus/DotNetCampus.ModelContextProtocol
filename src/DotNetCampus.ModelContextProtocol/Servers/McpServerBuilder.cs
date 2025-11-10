using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Core;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 用于构建 MCP 服务器的生成器。
/// </summary>
public class McpServerBuilder
{
    private McpServerContext? _context;
    private HttpServerTransportOptions? _httpOptions;

    /// <summary>
    /// 配置 MCP 服务器的工具和相关选项。
    /// </summary>
    /// <param name="toolsBuilder">用于配置工具的生成器。</param>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    public McpServerBuilder WithTools(Action<McpServerToolsBuilder> toolsBuilder)
    {
        var builder = new McpServerToolsBuilder(_context);
        toolsBuilder(builder);
        _context = builder.Context;
        return this;
    }

    /// <summary>
    /// 允许此 MCP 服务器通过标准输入/输出流提供服务。
    /// </summary>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    public McpServerBuilder WithStdio()
    {
        return this;
    }

    /// <summary>
    /// 允许此 MCP 服务器通过 HTTP 提供服务。
    /// </summary>
    /// <param name="port">MCP 服务器将监听 http://localhost:{port} 上的请求。</param>
    /// <param name="endpoint">
    /// MCP 服务器将监听的路由端点，例如指定为 mcp 时，完整的 URL 为 http://localhost:{port}/mcp。<br/>
    /// 所有的 MCP 请求都将发送到该端点；除非客户端使用旧版本（2024-11-05）的 SSE 协议传输时，会自动改为使用 /mcp/sse 端点。<br/>
    /// </param>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    public McpServerBuilder WithHttp(int port, [StringSyntax("Route")] string endpoint)
    {
        _httpOptions = _httpOptions switch
        {
            null => new HttpServerTransportOptions
            {
                BaseUrl = $"http://localhost:{port}",
                Endpoint = endpoint,
            },
            var o => new HttpServerTransportOptions
            {
                BaseUrl = $"http://localhost:{port}",
                Endpoint = endpoint,
            },
        };
        return this;
    }

    /// <summary>
    /// 构建 MCP 服务器实例。
    /// </summary>
    /// <returns>构建的 MCP 服务器实例。</returns>
    public McpServer Build()
    {
        var context = _context ?? new McpServerContext();
        var list = new List<HttpServerTransport>();
        if (_httpOptions is not null)
        {
            list.Add(new HttpServerTransport(
                context,
                _httpOptions));
        }
        return new McpServer(list);
    }
}

public class McpServerToolsBuilder(McpServerContext? originalContext)
{
    internal McpServerContext? Context { get; private set; } = originalContext;

    public McpServerToolsBuilder WithJsonSerializer(IMcpServerToolJsonSerializer jsonSerializer)
    {
        Context = Context switch
        {
            null => new McpServerContext
            {
                JsonSerializer = jsonSerializer,
            },
            var c => c with
            {
                JsonSerializer = jsonSerializer,
            },
        };
        return this;
    }

    public McpServerToolsBuilder WithJsonSerializer(JsonSerializerContext generatedJsonSerializerContext)
    {
        return WithJsonSerializer(new McpServerToolJsonSerializer(generatedJsonSerializerContext));
    }
}
