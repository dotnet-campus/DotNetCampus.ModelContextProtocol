namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// MCP 协议相关的异常基类。
/// </summary>
public class ModelContextProtocolException : Exception
{
    public ModelContextProtocolException()
    {
    }

    public ModelContextProtocolException(string message)
        : base(message)
    {
    }

    public ModelContextProtocolException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
