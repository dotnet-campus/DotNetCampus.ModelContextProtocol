using System.Diagnostics.CodeAnalysis;
using DotNetCampus.Logging;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP 服务器的上下文信息。
/// </summary>
public record McpServerContext
{
    /// <summary>
    /// 用于 DotNetCampus.ModelContextProtocol 库内部记录日志的记录器。
    /// </summary>
    [NotNull]
    internal ILogger? Logger
    {
        get => field ??= Log.Current;
        init;
    }

    /// <summary>
    /// 当前的日志级别。<br/>
    /// 只有达到或高于此级别的日志消息才应该被发送给客户端。<br/>
    /// Current logging level.<br/>
    /// Only log messages at or above this level should be sent to the client.
    /// </summary>
    public LoggingLevel? ClientLoggingLevel { get; internal set; }

    /// <summary>
    /// 当前用于序列化和反序列化 MCP 工具参数和结果的 JSON 序列化器的类型名称。<br/>
    /// 此属性用于报告更准确的异常信息。
    /// </summary>
    public string? JsonSerializerTypeName { get; internal init; }

    /// <summary>
    /// 用于序列化和反序列化 MCP 工具参数和结果的 JSON 序列化器。
    /// </summary>
    [NotNull]
    public IMcpServerToolJsonSerializer? JsonSerializer
    {
        get => field ??= new McpServerToolJsonSerializer();
        internal init;
    }

    /// <summary>
    /// 为 MCP 工具参数提供依赖注入。
    /// </summary>
    public IServiceProvider? ServiceProvider { get; internal init; }

    /// <summary>
    /// 处理 MCP 协议中的所有来自客户端请求的方法调用。
    /// </summary>
    [NotNull]
    internal McpRequestHandlers? Handlers
    {
        get => field ?? throw new InvalidOperationException("Handlers 未被设置。");
        set => field = field switch
        {
            null => value,
            _ => throw new InvalidOperationException("Handlers 已经被设置，不能重复设置。"),
        };
    }

    /// <summary>
    /// 指示是否启用调试模式。<br/>
    /// 启用后会记录或报告更多调试信息，这些调试信息甚至会通过 MCP 协议传输到客户端。
    /// </summary>
    public bool IsDebugMode { get; internal set; }
}
