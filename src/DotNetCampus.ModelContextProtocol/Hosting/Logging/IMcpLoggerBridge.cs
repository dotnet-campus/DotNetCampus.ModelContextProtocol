namespace DotNetCampus.ModelContextProtocol.Hosting.Logging;

/// <summary>
/// 仅由 .NET 库类型构成的日志桥。用于源生成器将无依赖库中的日志重定向到应用程序聚合日志系统中。
/// </summary>
public interface IMcpLoggerBridge
{
    /// <summary>
    /// 确定指定的日志级别是否已启用。
    /// </summary>
    /// <param name="logLevel">要检查的日志级别。</param>
    /// <returns>如果启用该级别，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
    bool IsEnabled(int logLevel)
#if NETCOREAPP3_0_OR_GREATER
        => true
#endif
    ;

    /// <summary>
    /// 写入日志条目。
    /// </summary>
    /// <param name="logLevel">将在此级别上写入条目。</param>
    /// <param name="eventId">事件的 Id。</param>
    /// <param name="eventName">事件的名称。</param>
    /// <param name="state">要写入的条目。也可以是一个对象。</param>
    /// <param name="exception">与此条目相关的异常。</param>
    /// <param name="formatter">创建一条字符串消息以记录 <paramref name="state" /> 和 <paramref name="exception" />。</param>
    /// <typeparam name="TState">要写入的对象的类型。</typeparam>
    void Log<TState>(
        int logLevel,
        int eventId,
        string? eventName,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter);

#if !NETCOREAPP3_0_OR_GREATER
}

/// <summary>
/// 提供一个静态类，用于连接日志桥到当前的桥接日志记录器中。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LoggerBridgeLinker
{
#endif

    /// <summary>
    /// 获取当前的日志桥。
    /// </summary>
    internal static IMcpLoggerBridge? Bridge;

    /// <summary>
    /// 连接一个日志桥到当前的桥接日志记录器中。
    /// 这样，所有使用此桥接日志记录器记录的日志会全部被重定向到日志桥中。
    /// </summary>
    /// <param name="bridge">要连接此日志记录器的日志桥。</param>
    public static void Link(IMcpLoggerBridge bridge)
    {
        Bridge = bridge;
    }
}
