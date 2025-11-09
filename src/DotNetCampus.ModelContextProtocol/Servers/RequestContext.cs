namespace DotNetCampus.ModelContextProtocol.Servers;

public sealed class RequestContext<TParams>(TParams? @params)
{
    public TParams? Params { get; } = @params;
}
