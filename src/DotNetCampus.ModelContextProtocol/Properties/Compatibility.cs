#pragma warning disable CS9113

#if NET7_0_OR_GREATER
#else
namespace System.Diagnostics
{
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
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class StringSyntaxAttribute(string syntax) : Attribute;
}

#endif
