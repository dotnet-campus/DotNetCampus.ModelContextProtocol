using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Servers;

public interface IMcpServerToolsProvider : IReadOnlyCollection<IMcpServerTool>
{
    bool TryGet(string toolName, [NotNullWhen(true)] out IMcpServerTool? tool);

    TTool GetOrAddSingleton<TTool>(string id, Func<TTool> toolFactory)
        where TTool : notnull;
}

internal sealed class McpServerToolsProvider : IMcpServerToolsProvider
{
    private readonly Dictionary<string, IMcpServerTool> _tools = [];
    private readonly Dictionary<string, object> _toolContainerInstances = [];

    public int Count => _tools.Count;

    public bool TryAdd(string name, IMcpServerTool tool)
    {
        return _tools.TryAdd(name, tool);
    }

    public bool TryGet(string toolName, [NotNullWhen(true)] out IMcpServerTool? tool)
    {
        return _tools.TryGetValue(toolName, out tool);
    }

    public T GetOrAddSingleton<T>(string containerId, Func<T> toolContainerFactory)
        where T : notnull
    {
        if (_toolContainerInstances.TryGetValue(containerId, out var existingToolContainer))
        {
            return (T)existingToolContainer;
        }

        var toolContainer = toolContainerFactory();
        _toolContainerInstances[containerId] = toolContainer;
        return toolContainer;
    }

    public IEnumerator<IMcpServerTool> GetEnumerator()
    {
        return _tools.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
