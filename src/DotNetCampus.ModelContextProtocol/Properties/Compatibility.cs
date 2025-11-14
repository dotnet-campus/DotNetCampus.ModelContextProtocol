namespace DotNetCampus.ModelContextProtocol.Properties;
#pragma warning disable CS9113

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

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
internal sealed class StringSyntaxAttribute(string syntax) : Attribute;

#endif