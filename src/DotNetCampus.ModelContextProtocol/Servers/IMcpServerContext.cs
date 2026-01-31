using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 包含 MCP 服务器的上下文信息。
/// </summary>
public interface IMcpServerContext
{
    /// <summary>
    /// 专门提供给 MCP 库内部代码和外部扩展使用的日志记录器。
    /// </summary>
    IMcpLogger Logger { get; }

    /// <summary>
    /// 用于序列化和反序列化 MCP 工具参数和结果的 JSON 序列化器。
    /// </summary>
    IMcpServerToolJsonSerializer? JsonSerializer { get; }

    /// <summary>
    /// 当前用于序列化和反序列化 MCP 工具参数和结果的 JSON 序列化器的类型名称。<br/>
    /// 此属性用于报告更准确的异常信息。
    /// </summary>
    string? JsonSerializerTypeName { get; }

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

    /// <summary>
    /// 获取请求处理器。<br/>
    /// 通过继承 <see cref="McpServerRequestHandlers"/> 并重写方法，可以实现自定义的请求处理、记录或扩展功能。
    /// </summary>
    McpServerRequestHandlers? Handlers { get; }
}
