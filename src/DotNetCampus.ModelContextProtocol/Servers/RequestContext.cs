using DotNetCampus.ModelContextProtocol.Core;

namespace DotNetCampus.ModelContextProtocol.Servers;

public sealed class RequestContext<TParams>
{
    internal RequestContext(ScopedServiceProvider services, TParams? @params)
    {
        Services = services;
        Params = @params;
    }

    internal ScopedServiceProvider Services { get; }

    public TParams? Params { get; }
}
