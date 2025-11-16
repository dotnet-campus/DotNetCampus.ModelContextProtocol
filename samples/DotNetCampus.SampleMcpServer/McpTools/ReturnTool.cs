using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.SampleMcpServer.McpTools;

/// <summary>
/// 本 MCP 工具用于测试各种不同类型的返回值功能。
/// </summary>
public class ReturnTool
{
    /// <summary>
    /// 获取当前本地时间。
    /// </summary>
    /// <returns></returns>
    [McpServerTool]
    public LocalTimeInfo GetCurrentTime()
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

public record LocalTimeInfo
{
    public int Year { get; init; }
    public int Month { get; init; }
    public int Day { get; init; }
    public int Hour { get; init; }
    public int Minute { get; init; }
    public int Second { get; init; }
}
