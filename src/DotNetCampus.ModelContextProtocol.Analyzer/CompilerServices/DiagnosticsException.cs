using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 表示源生成器在代码生成过程中遇到的诊断错误。
/// 通过 catch 此异常并调用 <see cref="CreateDiagnostic"/> 即可将错误报告给编译器。
/// </summary>
public class DiagnosticsException : Exception
{
    public DiagnosticsException(DiagnosticDescriptor descriptor, Location location, params object?[] messageArgs)
        : base(descriptor.MessageFormat.ToString())
    {
        Descriptor = descriptor;
        Location = location;
        MessageArgs = messageArgs;
    }

    public DiagnosticDescriptor Descriptor { get; }
    public Location Location { get; }
    public object?[] MessageArgs { get; }

    public Diagnostic CreateDiagnostic() => Diagnostic.Create(Descriptor, Location, MessageArgs);
}
