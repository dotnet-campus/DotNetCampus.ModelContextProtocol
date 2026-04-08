using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Servers;
using DotNetCampus.ModelContextProtocol.Transports;

namespace DotNetCampus.ModelContextProtocol.Hosting.Services;

/// <summary>
/// 提供向 <see cref="IMcpServiceCollection"/> 注册传输层会话服务的扩展方法。<br/>
/// Extension methods for registering transport session services into <see cref="IMcpServiceCollection"/>.
/// </summary>
public static class McpServiceCollectionTransportExtensions
{
    /// <summary>
    /// 向 MCP 服务集合中注册传输层会话相关服务，包括
    /// <see cref="IServerTransportSession"/> 和 <see cref="IMcpServerSampling"/>。<br/>
    /// Registers transport session services into the MCP service collection,
    /// including <see cref="IServerTransportSession"/> and <see cref="IMcpServerSampling"/>.
    /// </summary>
    /// <param name="services">MCP 服务集合。The MCP service collection.</param>
    /// <param name="session">当前传输层会话实例。The current transport session instance.</param>
    /// <param name="logger">日志记录器，传递给 Sampling 实现。</param>
    /// <returns>提供链式调用的服务集合。The service collection for chaining.</returns>
    public static IMcpServiceCollection AddTransportSession(this IMcpServiceCollection services, IServerTransportSession session, IMcpLogger logger)
    {
        services.AddScoped<IServerTransportSession>(session);
        services.AddScoped<IMcpServerSampling>(new McpServerSampling(session, logger));
        return services;
    }
}
