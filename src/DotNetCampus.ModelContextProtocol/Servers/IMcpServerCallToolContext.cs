using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Exceptions;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 包含 MCP 服务器收到来自客户端的工具调用时，服务端调用工具具体实现可能会用到的各种上下文信息。<br/>
/// Contains various context information that the server-side implementation of the MCP server tool
/// may use when the MCP server receives a tool invocation from the client.
/// </summary>
public interface IMcpServerCallToolContext
{
    /// <summary>
    /// 调用工具的 MCP 服务器实例。<br/>
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

    /// <summary>
    /// 尝试从依赖注入容器中解析并获取指定类型的服务实例。
    /// </summary>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <returns>指定类型的服务实例。</returns>
    T? TryGetMcpToolService<T>()
    {
        return (T?)Services.GetService(typeof(T));
    }

    /// <summary>
    /// 从依赖注入容器中解析并获取指定类型的服务实例。
    /// </summary>
    /// <param name="sourceGeneratedServiceTypeName">由源生成器提供的要解析的服务类型名称。</param>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <returns>指定类型的服务实例。</returns>
    /// <exception cref="McpToolServiceNotFoundException">当指定类型的服务在依赖注入容器中未找到时引发。</exception>
    T GetRequiredMcpToolService<T>(string sourceGeneratedServiceTypeName)
    {
        return (T?)Services.GetService(typeof(T))
               ?? throw new McpToolServiceNotFoundException(sourceGeneratedServiceTypeName);
    }
}

internal sealed class McpServerCallToolContext : IMcpServerCallToolContext
{
    public required McpServer McpServer { get; init; }
    public required IServiceProvider Services { get; init; }
    public required JsonSerializerContext JsonSerializerContext { get; init; }
    public required JsonElement InputJsonArguments { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
