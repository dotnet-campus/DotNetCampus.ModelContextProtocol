using System.Text.Json;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 请求上下文。
/// </summary>
/// <typeparam name="TParams">请求参数类型。</typeparam>
public sealed class RequestContext<TParams>
{
    internal RequestContext(IServiceProvider services, JsonElement? rawParams, TParams? @params)
    {
        Services = services;
        RawParams = rawParams;
        Params = @params;
    }

    internal IServiceProvider Services { get; }

    /// <summary>
    /// 获取原始的请求参数。
    /// </summary>
    public JsonElement? RawParams { get; }

    /// <summary>
    /// 获取请求参数。
    /// </summary>
    public TParams? Params { get; }
}
