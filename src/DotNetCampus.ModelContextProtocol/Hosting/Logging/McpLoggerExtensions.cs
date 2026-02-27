using System.Runtime.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Hosting.Logging;

/// <summary>
/// <see cref="IMcpLogger"/> 的常见场景的扩展方法。
/// </summary>
public static class McpLoggerExtensions
{
    /// <param name="logger">记录日志所使用的记录器。</param>
    extension(IMcpLogger logger)
    {
        /// <summary>
        /// 记录详细的调试信息。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        public void Debug(DefaultInterpolatedStringHandler message)
        {
            if (!logger.IsEnabled(LoggingLevel.Debug))
            {
                return;
            }

            logger.Log(LoggingLevel.Debug, message.ToStringAndClear(), null, static (s, ex) => s);
        }

        /// <summary>
        /// 记录一般信息。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        public void Info(DefaultInterpolatedStringHandler message)
        {
            if (!logger.IsEnabled(LoggingLevel.Info))
            {
                return;
            }

            logger.Log(LoggingLevel.Info, message.ToStringAndClear(), null, static (s, ex) => s);
        }

        /// <summary>
        /// 记录正常但重要的消息。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        public void Notice(DefaultInterpolatedStringHandler message)
        {
            if (!logger.IsEnabled(LoggingLevel.Notice))
            {
                return;
            }

            logger.Log(LoggingLevel.Notice, message.ToStringAndClear(), null, static (s, ex) => s);
        }

        /// <summary>
        /// 记录警告信息。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        /// <param name="exception">如果有异常信息，可以传入此参数。</param>
        public void Warn(DefaultInterpolatedStringHandler message, Exception? exception = null)
        {
            if (!logger.IsEnabled(LoggingLevel.Warning))
            {
                return;
            }

            logger.Log(LoggingLevel.Warning, message.ToStringAndClear(), exception, static (s, ex) => s);
        }

        /// <summary>
        /// 记录错误信息。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        /// <param name="exception">如果有异常信息，可以传入此参数。</param>
        public void Error(DefaultInterpolatedStringHandler message, Exception? exception = null)
        {
            if (!logger.IsEnabled(LoggingLevel.Error))
            {
                return;
            }

            logger.Log(LoggingLevel.Error, message.ToStringAndClear(), exception, static (s, ex) => s);
        }

        /// <summary>
        /// 记录严重错误信息。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        /// <param name="exception">如果有异常信息，可以传入此参数。</param>
        public void Critical(DefaultInterpolatedStringHandler message, Exception? exception = null)
        {
            if (!logger.IsEnabled(LoggingLevel.Critical))
            {
                return;
            }

            logger.Log(LoggingLevel.Critical, message.ToStringAndClear(), exception, static (s, ex) => s);
        }

        /// <summary>
        /// 记录必须立即采取行动的日志。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        /// <param name="exception">如果有异常信息，可以传入此参数。</param>
        public void Alert(DefaultInterpolatedStringHandler message, Exception? exception = null)
        {
            if (!logger.IsEnabled(LoggingLevel.Alert))
            {
                return;
            }

            logger.Log(LoggingLevel.Alert, message.ToStringAndClear(), exception, static (s, ex) => s);
        }

        /// <summary>
        /// 记录系统不可用的日志。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        /// <param name="exception">如果有异常信息，可以传入此参数。</param>
        public void Emergency(DefaultInterpolatedStringHandler message, Exception? exception = null)
        {
            if (!logger.IsEnabled(LoggingLevel.Emergency))
            {
                return;
            }

            logger.Log(LoggingLevel.Emergency, message.ToStringAndClear(), exception, static (s, ex) => s);
        }
    }
}
