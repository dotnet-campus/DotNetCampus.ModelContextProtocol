using System.Diagnostics.CodeAnalysis;
using DotNetCampus.Logging;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Servers;

public record McpServerContext
{
    [NotNull]
    internal ILogger? Logger
    {
        get => field ??= Log.Current;
        init;
    }

    [NotNull]
    public IMcpServerToolJsonSerializer? JsonSerializer
    {
        get => field ??= new McpServerToolJsonSerializer();
        init;
    }

    [NotNull]
    internal McpRequestHandlerRegistry? Handlers
    {
        get => field ?? throw new InvalidOperationException("Handlers 未被设置。");
        set => field = field switch
        {
            null => value,
            _ => throw new InvalidOperationException("Handlers 已经被设置，不能重复设置。"),
        };
    }

    public bool IsDebugMode { get; internal set; }

    /// <summary>
    /// 当前的日志级别。<br/>
    /// 只有达到或高于此级别的日志消息才应该被发送给客户端。<br/>
    /// Current logging level.<br/>
    /// Only log messages at or above this level should be sent to the client.
    /// </summary>
    public LoggingLevel? LoggingLevel { get; internal set; }
}
