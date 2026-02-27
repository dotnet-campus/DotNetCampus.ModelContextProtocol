namespace DotNetCampus.ModelContextProtocol.Tests;

internal sealed class TestServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = [];

    public TestServiceProvider AddService<TService>(TService service)
        where TService : class
    {
        _services[typeof(TService)] = service;
        return this;
    }

    public object? GetService(Type serviceType)
    {
        return _services.GetValueOrDefault(serviceType);
    }
}

internal sealed record TestInjectedDependency(string Value);
