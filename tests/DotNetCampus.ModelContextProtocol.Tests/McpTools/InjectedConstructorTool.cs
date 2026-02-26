using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Tests.McpTools;

internal class InjectedConstructorTool(TestInjectedDependency dependency)
{
    [McpServerTool(Name = "di_echo", ReadOnly = true)]
    public string Echo(string message)
    {
        return $"{dependency.Value}:{message}";
    }
}
