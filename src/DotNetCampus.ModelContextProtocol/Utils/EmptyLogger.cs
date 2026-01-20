using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Utils;

internal sealed class EmptyLogger : IMcpLogger
{
    public static readonly EmptyLogger Instance = new();

    private EmptyLogger()
    {
    }

    public bool IsEnabled(LoggingLevel loggingLevel) => false;

    public void Log<TState>(LoggingLevel loggingLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }
}
