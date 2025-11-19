using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP 服务器原语提供程序接口。
/// </summary>
/// <typeparam name="TPrimitive">原语类型</typeparam>
public interface IMcpServerPrimitiveProvider<TPrimitive> : IReadOnlyCollection<TPrimitive> where TPrimitive : class
{
    /// <summary>
    /// 尝试获取指定名称的原语。
    /// </summary>
    bool TryGet(string name, [NotNullWhen(true)] out TPrimitive? primitive);

    /// <summary>
    /// 获取或添加单例。
    /// </summary>
    T GetOrAddSingleton<T>(string id, Func<T> factory)
        where T : notnull;
}

/// <summary>
/// MCP 服务器资源提供程序接口。
/// </summary>
public interface IMcpServerResourcesProvider : IMcpServerPrimitiveProvider<IMcpServerResource>
{
    /// <summary>
    /// 尝试根据 URI 路由到匹配的资源，并提取参数。<br/>
    /// Try to route to a matching resource by URI and extract parameters.
    /// </summary>
    /// <param name="uri">要匹配的 URI。The URI to match.</param>
    /// <param name="resource">匹配的资源。The matched resource.</param>
    /// <param name="parameters">从 URI 中提取的参数（如果是模板资源）。Parameters extracted from the URI (for template resources).</param>
    /// <returns>如果找到匹配的资源，返回 true；否则返回 false。True if a matching resource is found; otherwise false.</returns>
    bool TryRoute(string uri, [NotNullWhen(true)] out IMcpServerResource? resource, out Dictionary<string, string>? parameters);

    /// <summary>
    /// 获取所有静态资源（用于 resources/list）。<br/>
    /// Get all static resources (for resources/list).
    /// </summary>
    IEnumerable<IMcpServerResource> GetStaticResources();

    /// <summary>
    /// 获取所有模板资源（用于 resources/templates/list）。<br/>
    /// Get all template resources (for resources/templates/list).
    /// </summary>
    IEnumerable<IMcpServerResource> GetTemplateResources();
}

/// <summary>
/// MCP 服务器工具提供程序接口。
/// </summary>
public interface IMcpServerToolsProvider : IMcpServerPrimitiveProvider<IMcpServerTool>;

internal sealed class McpServerResourcesProvider : McpServerPrimitiveProvider<IMcpServerResource>, IMcpServerResourcesProvider
{
    private readonly UriTemplateRouter<IMcpServerResource> _router = new();

    /// <inheritdoc />
    public bool TryRoute(string uri, [NotNullWhen(true)] out IMcpServerResource? resource, out Dictionary<string, string>? parameters)
    {
        return _router.TryMatch(uri, out resource, out parameters);
    }

    /// <inheritdoc />
    public IEnumerable<IMcpServerResource> GetStaticResources()
    {
        return _router.GetAllResources();
    }

    /// <inheritdoc />
    public IEnumerable<IMcpServerResource> GetTemplateResources()
    {
        return _router.GetAllTemplates();
    }

    /// <summary>
    /// 添加资源到路由器。<br/>
    /// Add a resource to the router.
    /// </summary>
    public new bool TryAdd(string name, IMcpServerResource resource)
    {
        // 先添加到基类字典（按名称索引）
        if (!base.TryAdd(name, resource))
        {
            return false;
        }

        // 添加到 URI 路由器
        if (resource.IsTemplate)
        {
            _router.AddTemplate(resource.UriTemplate, resource);
        }
        else
        {
            _router.AddExactUri(resource.UriTemplate, resource);
        }

        return true;
    }
}

internal sealed class McpServerToolsProvider : McpServerPrimitiveProvider<IMcpServerTool>, IMcpServerToolsProvider;

internal abstract class McpServerPrimitiveProvider<TPrimitive> : IMcpServerPrimitiveProvider<TPrimitive> where TPrimitive : class
{
    private readonly Dictionary<string, TPrimitive> _primitives = [];
    private readonly Dictionary<string, object> _containerInstances = [];

    public int Count => _primitives.Count;

    public bool TryAdd(string name, TPrimitive primitive)
    {
        return _primitives.TryAdd(name, primitive);
    }

    public bool TryGet(string name, [NotNullWhen(true)] out TPrimitive? primitive)
    {
        return _primitives.TryGetValue(name, out primitive);
    }

    public T GetOrAddSingleton<T>(string containerId, Func<T> factory)
        where T : notnull
    {
        if (_containerInstances.TryGetValue(containerId, out var existingContainer))
        {
            return (T)existingContainer;
        }

        var instance = factory();
        _containerInstances[containerId] = instance;
        return instance;
    }

    public IEnumerator<TPrimitive> GetEnumerator()
    {
        return _primitives.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
