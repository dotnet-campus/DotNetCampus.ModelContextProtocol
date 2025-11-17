using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Servers;

public interface IMcpServerPrimitiveProvider<TPrimitive> : IReadOnlyCollection<TPrimitive> where TPrimitive : class
{
    bool TryGet(string name, [NotNullWhen(true)] out TPrimitive? primitive);

    T GetOrAddSingleton<T>(string id, Func<T> factory)
        where T : notnull;
}

public interface IMcpServerResourcesProvider : IMcpServerPrimitiveProvider<IMcpServerResource>;

public interface IMcpServerToolsProvider : IMcpServerPrimitiveProvider<IMcpServerTool>;

internal sealed class McpServerResourcesProvider : McpServerPrimitiveProvider<IMcpServerResource>, IMcpServerResourcesProvider;

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
