using System.Collections.Concurrent;

namespace DotNetCampus.ModelContextProtocol.Hosting.Services;

/// <summary>
/// MCP 库内部简易依赖注入框架。
/// </summary>
/// <param name="fallbackServiceProvider">外部注入的服务提供者。</param>
internal class ScopedServiceProvider(IServiceProvider? fallbackServiceProvider) : IMcpServiceCollection, IServiceProvider
{
    private readonly ConcurrentDictionary<Type, object> _scopedInstances = [];
    private readonly Dictionary<Type, Func<IServiceProvider, object>> _services = [];

    public object? GetService(Type serviceType)
    {
        if (_scopedInstances.TryGetValue(serviceType, out var instance))
        {
            return instance;
        }

        if (_services.TryGetValue(serviceType, out var factory))
        {
            var createdInstance = factory(this);
            _scopedInstances[serviceType] = createdInstance;
            return createdInstance;
        }

        return fallbackServiceProvider?.GetService(serviceType);
    }

    public IMcpServiceCollection AddScoped<T>(T instance)
    {
        _scopedInstances[typeof(T)] = instance!;
        return this;
    }

    public IMcpServiceCollection AddScoped<T>(Func<IServiceProvider, T> implementationFactory)
    {
        _services[typeof(T)] = s => implementationFactory(s)!;
        return this;
    }
}
