using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Tests.McpResources;

internal class InjectedConstructorResource(TestInjectedDependency dependency)
{
    [McpServerResource(UriTemplate = "test://di/info", Name = "DI Info")]
    public string GetInfo()
    {
        return $"resource:{dependency.Value}";
    }
}
