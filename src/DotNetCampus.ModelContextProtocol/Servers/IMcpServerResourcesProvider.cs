using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP 服务器资源提供程序接口。<br/>
/// 负责管理和路由 MCP 资源，支持静态资源和 URI 模板资源。<br/>
/// MCP server resource provider interface.<br/>
/// Manages and routes MCP resources, supporting both static resources and URI template resources.
/// </summary>
public interface IMcpServerResourcesProvider : IReadOnlyCollection<IMcpServerResource>
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

    /// <summary>
    /// 获取或添加单例容器实例。<br/>
    /// Get or add a singleton container instance.
    /// </summary>
    T GetOrAddSingleton<T>(string containerId, Func<McpServer, T> factory) where T : notnull;
}

/// <summary>
/// MCP 服务器资源提供程序实现。<br/>
/// MCP server resource provider implementation.
/// </summary>
internal sealed class McpServerResourcesProvider : IMcpServerResourcesProvider
{
    private readonly UriTemplateRouter<IMcpServerResource> _router = new();
    private readonly Dictionary<string, object> _containerInstances = [];
    private int _count;

    /// <inheritdoc />
    public int Count => _count;

    /// <summary>
    /// 提供给 <see cref="McpServerBuilder"/> 调用，当部分 MCP 资源需要注入时，可使用此属性进行注入。
    /// </summary>
    internal McpServer Server
    {
        get => field ?? throw new InvalidOperationException("MCP 服务实例未被设置，这应该是 McpServerBuilder 实现的错误。");
        set => field = field switch
        {
            null => value,
            _ => throw new InvalidOperationException("MCP 服务实例已被设置，不可重复设置。"),
        };
    }

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

    /// <inheritdoc />
    public T GetOrAddSingleton<T>(string containerId, Func<McpServer, T> factory) where T : notnull
    {
        if (_containerInstances.TryGetValue(containerId, out var existingContainer))
        {
            return (T)existingContainer;
        }

        var instance = factory(Server);
        _containerInstances[containerId] = instance;
        return instance;
    }

    /// <summary>
    /// 添加资源到路由器。<br/>
    /// Add a resource to the router.
    /// </summary>
    public bool TryAdd(string name, IMcpServerResource resource)
    {
        // 添加到 URI 路由器
        var added = resource.IsTemplate
            ? TryAddTemplate(resource)
            : TryAddExact(resource);

        if (added)
        {
            _count++;
        }

        return added;
    }

    private bool TryAddExact(IMcpServerResource resource)
    {
        return _router.AddExactUri(resource.UriTemplate, resource);
    }

    private bool TryAddTemplate(IMcpServerResource resource)
    {
        _router.AddTemplate(resource.UriTemplate, resource);
        return true; // Template 总是成功添加（可能有多个模板）
    }

    /// <inheritdoc />
    public IEnumerator<IMcpServerResource> GetEnumerator()
    {
        foreach (var staticResource in GetStaticResources())
        {
            yield return staticResource;
        }
        foreach (var templateResource in GetTemplateResources())
        {
            yield return templateResource;
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
