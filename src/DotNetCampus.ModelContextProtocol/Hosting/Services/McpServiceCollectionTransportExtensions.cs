using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Servers;
using DotNetCampus.ModelContextProtocol.Transports;

namespace DotNetCampus.ModelContextProtocol.Hosting.Services;

/// <summary>
/// 提供向 <see cref="IMcpServiceCollection"/> 注册传输层会话服务的扩展方法。
/// </summary>
public static class McpServiceCollectionTransportExtensions
{
    /// <summary>
    /// 向 MCP 服务集合中注册传输层会话相关服务，包括
    /// <see cref="IServerTransportSession"/> 和 <see cref="IMcpServerSampling"/>。
    /// </summary>
    /// <param name="services">MCP 服务集合。</param>
    /// <param name="session">当前传输层会话实例。</param>
    /// <param name="logger">日志记录器，传递给 Sampling 实现。</param>
    /// <returns>提供链式调用的服务集合。</returns>
    public static IMcpServiceCollection AddTransportSession(this IMcpServiceCollection services, IServerTransportSession session, IMcpLogger logger)
    {
        services.AddScoped<IServerTransportSession>(session);
        services.AddScoped<IMcpServerSampling>(new McpServerSampling(session, logger));
        return services;
    }
}
