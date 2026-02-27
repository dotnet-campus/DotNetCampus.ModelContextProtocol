using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Hosting.Logging;

/// <summary>
/// 专门提供给 MCP 库内部代码和外部扩展使用的日志记录接口。
/// </summary>
/// <remarks>
/// 业务代码可以轻松地将日志记录适配到现有的日志记录框架中。
/// </remarks>
public interface IMcpLogger
{
    /// <summary>
    /// 检查是否已启用给定的日志级别。
    /// </summary>
    /// <param name="loggingLevel"></param>
    /// <returns></returns>
    bool IsEnabled(LoggingLevel loggingLevel);

    /// <summary>
    /// 写入日志条目。
    /// </summary>
    /// <param name="loggingLevel">将在此级别上写入条目。</param>
    /// <param name="state">要写入的条目。也可以是一个对象。</param>
    /// <param name="exception">与此条目相关的异常。</param>
    /// <param name="formatter">创建一条字符串消息以记录 <paramref name="state" /> 和 <paramref name="exception" />。</param>
    /// <typeparam name="TState">要写入的对象的类型。</typeparam>
    void Log<TState>(
        LoggingLevel loggingLevel,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter);
}
