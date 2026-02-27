#pragma warning disable CS9113

#if NET7_0_OR_GREATER
#else
namespace System.IO
{
    internal static class CompatibilityExtensions
    {
        extension(StreamReader reader)
        {
            internal async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
            {
                return await reader.ReadLineAsync();
            }
        }
    }
}

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
    internal sealed class StringSyntaxAttribute(string syntax) : Attribute
    {
        /// <summary>The syntax identifier for strings containing composite formats for string formatting.</summary>
        public const string CompositeFormat = "CompositeFormat";

        /// <summary>The syntax identifier for strings containing date format specifiers.</summary>
        public const string DateOnlyFormat = "DateOnlyFormat";

        /// <summary>The syntax identifier for strings containing date and time format specifiers.</summary>
        public const string DateTimeFormat = "DateTimeFormat";

        /// <summary>The syntax identifier for strings containing <see cref="Enum" /> format specifiers.</summary>
        public const string EnumFormat = "EnumFormat";

        /// <summary>The syntax identifier for strings containing <see cref="Guid" /> format specifiers.</summary>
        public const string GuidFormat = "GuidFormat";

        /// <summary>The syntax identifier for strings containing JavaScript Object Notation (JSON).</summary>
        public const string Json = "Json";

        /// <summary>The syntax identifier for strings containing numeric format specifiers.</summary>
        public const string NumericFormat = "NumericFormat";

        /// <summary>The syntax identifier for strings containing regular expressions.</summary>
        public const string Regex = "Regex";

        /// <summary>The syntax identifier for strings containing time format specifiers.</summary>
        public const string TimeOnlyFormat = "TimeOnlyFormat";

        /// <summary>The syntax identifier for strings containing <see cref="TimeSpan" /> format specifiers.</summary>
        public const string TimeSpanFormat = "TimeSpanFormat";

        /// <summary>The syntax identifier for strings containing URIs.</summary>
        public const string Uri = "Uri";

        /// <summary>The syntax identifier for strings containing XML.</summary>
        public const string Xml = "Xml";
    }
}

#endif
