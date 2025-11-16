using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.SampleMcpServer.McpTools;

/// <summary>
/// 本 MCP 工具用于测试各种不同类型的输出功能。
/// </summary>
public class OutputTool
{
    /// <summary>
    /// 测试什么也不输出
    /// </summary>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public async Task TestNoReturn()
    {
        await Task.Yield();
    }

    /// <summary>
    /// 测试获取结构化的输出信息
    /// </summary>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public LocalTimeInfo TestStructureReturn()
    {
        var now = DateTime.Now;
        return new LocalTimeInfo
        {
            Year = now.Year,
            Month = now.Month,
            Day = now.Day,
            Hour = now.Hour,
            Minute = now.Minute,
            Second = now.Second,
        };
    }
}

/// <summary>
/// 表示本地时间的信息结构
/// </summary>
public record LocalTimeInfo
{
    /// <summary>
    /// 年份
    /// </summary>
    public int Year { get; init; }

    /// <summary>
    /// 月份
    /// </summary>
    public int Month { get; init; }

    /// <summary>
    /// 日期
    /// </summary>
    public int Day { get; init; }

    /// <summary>
    /// 小时
    /// </summary>
    public int Hour { get; init; }

    /// <summary>
    /// 分钟
    /// </summary>
    public int Minute { get; init; }

    /// <summary>
    /// 秒数
    /// </summary>
    public int Second { get; init; }
}
