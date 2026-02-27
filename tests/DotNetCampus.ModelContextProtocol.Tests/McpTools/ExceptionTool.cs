using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Tests.McpTools;

/// <summary>
/// 异常测试工具，用于测试工具执行时抛出异常的场景。
/// </summary>
public class ExceptionTool
{
    /// <summary>
    /// 总是抛出异常的工具。
    /// </summary>
    /// <param name="message">异常消息（可选）。</param>
    /// <returns>永远不会返回，总是抛出异常。</returns>
    [McpServerTool]
    public string ThrowError(string? message = null)
    {
        throw new InvalidOperationException(message ?? "Test exception from ThrowError tool.");
    }

    /// <summary>
    /// 抛出带有内部异常的嵌套异常。
    /// </summary>
    /// <returns>永远不会返回，总是抛出异常。</returns>
    [McpServerTool]
    public string ThrowNested()
    {
        try
        {
            throw new InvalidOperationException("Inner exception.");
        }
        catch (Exception ex)
        {
            throw new AggregateException("Outer exception.", ex);
        }
    }
}
