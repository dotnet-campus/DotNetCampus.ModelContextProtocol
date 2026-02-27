using DotNetCampus.ModelContextProtocol.Hosting.Services;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 请求上下文。
/// </summary>
/// <typeparam name="TParams">请求参数类型。</typeparam>
public sealed class RequestContext<TParams>
{
    internal RequestContext(IServiceProvider services, TParams? @params)
    {
        Services = services;
        Params = @params;
    }

    internal IServiceProvider Services { get; }

    /// <summary>
    /// 获取请求参数。
    /// </summary>
    public TParams? Params { get; }
}
