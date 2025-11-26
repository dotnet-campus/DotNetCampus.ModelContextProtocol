namespace DotNetCampus.ModelContextProtocol.Hosting.Services;

/// <summary>
/// 内部简易依赖注入容器内服务的集合。
/// </summary>
public interface IMcpServiceCollection
{
    /// <summary>
    /// 向服务集合中添加一个作用域服务实例。
    /// </summary>
    /// <param name="instance">服务实例。</param>
    /// <typeparam name="T">服务的类型。</typeparam>
    /// <returns>提供链式调用的服务集合。</returns>
    IMcpServiceCollection AddScoped<T>(T instance);

    /// <summary>
    /// 向服务集合中添加一个作用域服务。
    /// </summary>
    /// <param name="implementationFactory">服务的实现工厂。</param>
    /// <typeparam name="T">服务的类型。</typeparam>
    /// <returns>提供链式调用的服务集合。</returns>
    IMcpServiceCollection AddScoped<T>(Func<IServiceProvider, T> implementationFactory);
}
