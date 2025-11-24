using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Transports.Http;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 包含 MCP 服务器收到来自客户端的请求时，服务端处理请求具体实现可能会用到的各种上下文信息。<br/>
/// Contains various context information that the server-side implementation of the MCP server
/// may use when the MCP server receives a request from the client.
/// </summary>
public interface IMcpServerPrimitiveContext
{
    /// <summary>
    /// 调用 MCP 服务器实例。<br/>
    /// MCP server instance invoking the tool.
    /// </summary>
    McpServer McpServer { get; }

    /// <summary>
    /// 用于解析和获取服务的服务提供者。<br/>
    /// Service provider used to resolve and obtain services.
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// 可用于反序列化 MCP 工具调用输入参数的 JSON 序列化上下文。<br/>
    /// JSON serialization context that can be used to deserialize MCP tool invocation input parameters.
    /// </summary>
    JsonSerializerContext JsonSerializerContext { get; }
}

/// <summary>
/// 包含 MCP 服务器收到来自客户端的工具调用时，服务端调用工具具体实现可能会用到的各种上下文信息。<br/>
/// Contains various context information that the server-side implementation of the MCP server tool
/// may use when the MCP server receives a tool invocation from the client.
/// </summary>
public interface IMcpServerCallToolContext : IMcpServerPrimitiveContext
{
    /// <summary>
    /// 来自 MCP 协议中 tools/call 请求中 name 字段的工具名称。<br/>
    /// The name of the tool to call from the name field in the tools/call request in the MCP protocol.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 来自 MCP 协议中 tools/call 请求中 arguments 字段的 JSON 元素。<br/>
    /// JSON element from the arguments field in the tools/call request in the MCP protocol.
    /// </summary>
    JsonElement InputJsonArguments { get; }

    /// <summary>
    /// 用于取消工具调用操作的取消令牌。<br/>
    /// Cancellation token used to cancel the tool invocation operation.
    /// </summary>
    CancellationToken CancellationToken { get; }
}

/// <summary>
/// 包含 MCP 服务器收到来自客户端的读取资源时，服务端调用工具具体实现可能会用到的各种上下文信息。<br/>
/// Contains various context information that the server-side implementation of the MCP server resource
/// may use when the MCP server receives a read resource request from the client.
/// </summary>
public interface IMcpServerReadResourceContext : IMcpServerPrimitiveContext
{
    /// <summary>
    /// 要读取的资源的 URI。URI 可以使用任何协议；由服务器决定如何解释它。<br/>
    /// The URI of the resource to read. The URI can use any protocol; it is up to the server how to interpret it.
    /// </summary>
    [StringSyntax(StringSyntaxAttribute.Uri)]
    string Uri { get; }

    /// <summary>
    /// 资源的 MIME 类型（如 text/plain、application/json）。如果未设置，将根据资源内容自动推断。<br/>
    /// The MIME type of the resource (e.g., text/plain, application/json). If not set, it will be inferred from the resource contents.
    /// </summary>
    string? MimeType { get; }
}

internal sealed class McpServerCallToolContext : IMcpServerCallToolContext
{
    public required McpServer McpServer { get; init; }
    public required IServiceProvider Services { get; init; }
    public required JsonSerializerContext JsonSerializerContext { get; init; }
    public required string Name { get; init; }
    public required JsonElement InputJsonArguments { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}

internal sealed class McpServerReadResourceContext : IMcpServerReadResourceContext
{
    public required McpServer McpServer { get; init; }
    public required IServiceProvider Services { get; init; }
    public required JsonSerializerContext JsonSerializerContext { get; init; }
    public required string Uri { get; init; }
    public required string? MimeType { get; init; }
}

/// <summary>
/// 扩展 <see cref="IMcpServerCallToolContext"/> 接口的扩展方法。<br/>
/// Extension methods for the <see cref="IMcpServerCallToolContext"/> interface.
/// </summary>
public static class McpServerCallToolContextExtensions
{
    /// <param name="context">工具调用上下文。Tool invocation context.</param>
    extension(IMcpServerPrimitiveContext context)
    {
        /// <summary>
        /// 获取与 HTTP 传输相关的上下文信息（如果当前是通过 HTTP 传输的话）。<br/>
        /// Gets the context information related to HTTP transport (if the current transport is HTTP).
        /// </summary>
        public HttpServerTransportContext? HttpTransportContext => (HttpServerTransportContext?)context.Services.GetService(typeof(HttpServerTransportContext));
    }
}
