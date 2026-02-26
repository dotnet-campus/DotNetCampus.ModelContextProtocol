using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP 服务器工具提供程序接口。<br/>
/// 负责管理 MCP 工具，通过名称索引工具。<br/>
/// MCP server tool provider interface.<br/>
/// Manages MCP tools, indexing tools by name.
/// </summary>
public interface IMcpServerToolsProvider : IReadOnlyCollection<IMcpServerTool>
{
    /// <summary>
    /// 尝试获取指定名称的工具。<br/>
    /// Try to get a tool by name.
    /// </summary>
    /// <param name="name">工具名称。Tool name.</param>
    /// <param name="tool">找到的工具。The found tool.</param>
    /// <returns>如果找到工具，返回 true；否则返回 false。True if the tool is found; otherwise false.</returns>
    bool TryGet(string name, [NotNullWhen(true)] out IMcpServerTool? tool);

    /// <summary>
    /// 获取或添加单例容器实例。<br/>
    /// Get or add a singleton container instance.
    /// </summary>
    T GetOrAddSingleton<T>(string containerId, Func<McpServer, T> factory) where T : notnull;
}

/// <summary>
/// MCP 服务器工具提供程序实现。<br/>
/// MCP server tool provider implementation.
/// </summary>
internal sealed class McpServerToolsProvider : IMcpServerToolsProvider
{
    private readonly Dictionary<string, IMcpServerTool> _tools = [];
    private readonly Dictionary<string, object> _containerInstances = [];

    /// <inheritdoc />
    public int Count => _tools.Count;

    /// <summary>
    /// 提供给 <see cref="McpServerBuilder"/> 调用，当部分 MCP 工具需要注入时，可使用此属性进行注入。
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
    public bool TryGet(string name, [NotNullWhen(true)] out IMcpServerTool? tool)
    {
        return _tools.TryGetValue(name, out tool);
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
    /// 添加工具。<br/>
    /// Add a tool.
    /// </summary>
    public bool TryAdd(string name, IMcpServerTool tool)
    {
        return _tools.TryAdd(name, tool);
    }

    /// <inheritdoc />
    public IEnumerator<IMcpServerTool> GetEnumerator()
    {
        return _tools.Values.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
