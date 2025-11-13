namespace DotNetCampus.ModelContextProtocol.Exceptions;

/// <summary>
/// MCP 协议相关的异常基类。
/// </summary>
public class ModelContextProtocolException : Exception
{
    public ModelContextProtocolException(string message)
        : base(message)
    {
    }
}
