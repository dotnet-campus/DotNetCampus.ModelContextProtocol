using System.Collections.Concurrent;

namespace DotNetCampus.ModelContextProtocol.Utils;

/// <summary>
/// MCP 库内部简易依赖注入框架。
/// </summary>
/// <param name="fallbackServiceProvider">外部注入的服务提供者。</param>
internal class ScopedServiceProvider(IServiceProvider? fallbackServiceProvider) : IServiceCollection, IServiceProvider
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

    public IServiceCollection AddScoped<T>(T instance)
    {
        _scopedInstances[typeof(T)] = instance!;
        return this;
    }

    public IServiceCollection AddScoped<T>(Func<IServiceProvider, T> implementationFactory)
    {
        _services[typeof(T)] = s => implementationFactory(s)!;
        return this;
    }
}

/// <summary>
/// 内部简易依赖注入容器内服务的集合。
/// </summary>
internal interface IServiceCollection
{
    /// <summary>
    /// 向服务集合中添加一个作用域服务。
    /// </summary>
    /// <param name="implementationFactory">服务的实现工厂。</param>
    /// <typeparam name="T">服务的类型。</typeparam>
    /// <returns>提供链式调用的服务集合。</returns>
    IServiceCollection AddScoped<T>(Func<IServiceProvider, T> implementationFactory);
}
