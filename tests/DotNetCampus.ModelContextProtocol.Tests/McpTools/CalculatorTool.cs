using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Tests.McpTools;

/// <summary>
/// 计算器测试工具，用于测试基本的 Tool 调用。
/// </summary>
public class CalculatorTool
{
    /// <summary>
    /// 将两个整数相加。
    /// </summary>
    /// <param name="a">第一个加数。</param>
    /// <param name="b">第二个加数。</param>
    /// <returns>两数之和。</returns>
    [McpServerTool(ReadOnly = true)]
    public int Add(int a, int b)
    {
        return a + b;
    }

    /// <summary>
    /// 将两个整数相除。
    /// </summary>
    /// <param name="a">被除数。</param>
    /// <param name="b">除数。</param>
    /// <returns>商。</returns>
    [McpServerTool(ReadOnly = true)]
    public double Divide(int a, int b)
    {
        return (double)a / b;
    }
}
