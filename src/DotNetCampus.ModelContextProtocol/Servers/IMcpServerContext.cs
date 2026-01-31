using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 包含 MCP 服务器的上下文信息。
/// </summary>
public interface IMcpServerContext
{
    /// <summary>
    /// 用于序列化和反序列化 MCP 工具参数和结果的 JSON 序列化器。
    /// </summary>
    IMcpServerToolJsonSerializer? JsonSerializer { get; init; }

    /// <summary>
    /// 当前用于序列化和反序列化 MCP 工具参数和结果的 JSON 序列化器的类型名称。<br/>
    /// 此属性用于报告更准确的异常信息。
    /// </summary>
    string? JsonSerializerTypeName { get; }

    /// <summary>
    /// 允许收到来自客户端的请求处理完成后，将请求和响应全部提供给业务方进行记录。
    /// </summary>
    IMcpRequestTracer? Tracer { get; }

    /// <summary>
    /// 指示是否启用调试模式。<br/>
    /// 启用后会记录或报告更多调试信息，这些调试信息甚至会通过 MCP 协议传输到客户端。
    /// </summary>
    bool IsDebugMode { get; }

    /// <summary>
    /// 当前的日志级别。<br/>
    /// 只有达到或高于此级别的日志消息才应该被发送给客户端。
    /// </summary>
    LoggingLevel? McpLoggingLevel { get; set; }
}
