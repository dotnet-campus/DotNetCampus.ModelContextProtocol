using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Tests.McpTools;

internal class StatefulCounterTool
{
    private int _count;

    [McpServerTool(Name = "stateful_counter")]
    public int Next()
    {
        _count++;
        return _count;
    }
}
