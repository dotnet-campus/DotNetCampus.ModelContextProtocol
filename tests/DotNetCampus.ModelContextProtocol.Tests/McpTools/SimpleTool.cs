using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Tests.McpTools;

/// <summary>
/// This is a test tool for MCP initialization.
/// </summary>
public class SimpleTool
{
    /// <summary>
    /// Add number tool.
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>The sum of the two numbers.</returns>
    [McpServerTool(ReadOnly = true)]
    public double AddNumber(int a, double b)
    {
        return a + b;
    }
}
