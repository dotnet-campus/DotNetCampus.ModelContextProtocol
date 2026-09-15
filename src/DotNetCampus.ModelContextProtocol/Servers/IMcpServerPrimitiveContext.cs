using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Transports;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 包含 MCP 服务器收到来自客户端的请求时，服务端处理请求具体实现可能会用到的各种上下文信息。
/// </summary>
public interface IMcpServerPrimitiveContext
{
    /// <summary>
    /// 调用 MCP 服务器实例。
    /// </summary>
    McpServer McpServer { get; }

    /// <summary>
    /// 用于解析和获取服务的服务提供者。
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// 可用于反序列化 MCP 工具调用输入参数的 JSON 序列化上下文。
    /// </summary>
    JsonSerializerContext JsonSerializerContext { get; }

    /// <summary>
    /// 来自 MCP 协议中请求中 _meta 字段的元数据。
    /// </summary>
    JsonElement Meta { get; }
}

/// <summary>
/// 包含 MCP 服务器收到来自客户端的工具调用时，服务端调用工具具体实现可能会用到的各种上下文信息。
/// </summary>
public interface IMcpServerCallToolContext : IMcpServerPrimitiveContext
{
    /// <summary>
    /// 来自 MCP 协议中 tools/call 请求中 name 字段的工具名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 来自 MCP 协议中 tools/call 请求中 arguments 字段的 JSON 元素。
    /// </summary>
    JsonElement InputJsonArguments { get; }

    /// <summary>
    /// 用于取消工具调用操作的取消令牌。
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// 提供服务器向客户端发起 Sampling 请求的能力。始终非空；当传输层或客户端不支持 Sampling 时，<see cref="IMcpServerSampling.IsSupported"/> 为 <see langword="false"/>。
    /// </summary>
    IMcpServerSampling Sampling { get; }
}

/// <summary>
/// 包含 MCP 服务器收到来自客户端的读取资源时，服务端调用工具具体实现可能会用到的各种上下文信息。
/// </summary>
public interface IMcpServerReadResourceContext : IMcpServerPrimitiveContext
{
    /// <summary>
    /// 要读取的资源的 URI。URI 可以使用任何协议；由服务器决定如何解释它。
    /// </summary>
    [StringSyntax(StringSyntaxAttribute.Uri)]
    string Uri { get; }

    /// <summary>
    /// 资源的 MIME 类型（如 text/plain、application/json）。如果未设置，将根据资源内容自动推断。
    /// </summary>
    string? MimeType { get; }
}

internal sealed class McpServerCallToolContext : IMcpServerCallToolContext
{
    public required McpServer McpServer { get; init; }
    public required IServiceProvider Services { get; init; }
    public required JsonSerializerContext JsonSerializerContext { get; init; }
    public required JsonElement Meta { get; init; }
    public required string Name { get; init; }
    public required JsonElement InputJsonArguments { get; init; }
    public required CancellationToken CancellationToken { get; init; }

    public IMcpServerSampling Sampling =>
        (IMcpServerSampling?)Services.GetService(typeof(IMcpServerSampling))
        ?? NotSupportedMcpServerSampling.Instance;
}

internal sealed class McpServerReadResourceContext : IMcpServerReadResourceContext
{
    public required McpServer McpServer { get; init; }
    public required IServiceProvider Services { get; init; }
    public required JsonSerializerContext JsonSerializerContext { get; init; }
    public required JsonElement Meta { get; init; }
    public required string Uri { get; init; }
    public required string? MimeType { get; init; }
}

/// <summary>
/// 扩展 <see cref="IMcpServerCallToolContext"/> 接口的扩展方法。
/// </summary>
public static class McpServerCallToolContextExtensions
{
    /// <param name="context">工具调用上下文。</param>
    extension(IMcpServerPrimitiveContext context)
    {
        /// <summary>
        /// 获取当前请求对应的传输层会话。可用于获取会话 ID、客户端能力、协商协议版本和客户端信息等。
        /// </summary>
        public IServerTransportSession? TransportSession => (IServerTransportSession?)context.Services.GetService(typeof(IServerTransportSession));

        /// <summary>
        /// 获取与 HTTP 传输相关的上下文信息（如果当前是通过 HTTP 传输的话）。
        /// </summary>
        public HttpServerTransportContext? HttpTransportContext => (HttpServerTransportContext?)context.Services.GetService(typeof(HttpServerTransportContext));
    }
}
