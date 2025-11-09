global using DotNetCampus.ModelContextProtocol.Properties;

namespace DotNetCampus.ModelContextProtocol.Properties;

#if NET7_0_OR_GREATER
#else
internal sealed class UnreachableException : Exception
{
    public UnreachableException()
    {
    }

    public UnreachableException(string? message) : base(message)
    {
    }

    public UnreachableException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
#endif
