using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.SampleMcpServer.McpTools;

/// <summary>
/// 本 MCP 工具用于测试各种不同类型的输出功能。
/// </summary>
public class OutputTool
{
    /// <summary>
    /// 测试返回空字符串（MCP 工具必须有返回值，因此用空字符串代替 void）
    /// </summary>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public async Task<string> TestNoReturn()
    {
        await Task.Yield();
        return "";
    }

    /// <summary>
    /// 测试什么也不输出
    /// </summary>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public async Task<string?> TestNullableReturn()
    {
        return null;
    }

    /// <summary>
    /// 测试异步获取结构化的输出信息
    /// </summary>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public async Task<LocalTimeInfo> TestAsyncStructureReturn()
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

    /// <summary>
    /// 测试获取可空的结构化输出信息（必须显式 Structured = false，null 时框架进行破坏式回退）
    /// </summary>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true, Structured = false)]
    public LocalTimeInfo? TestNullableStructureReturn()
    {
        return null;
    }

    // /// <summary>
    // /// 解除注释后，此代码将报告编译错误
    // /// </summary>
    // /// <returns></returns>
    // [McpServerTool(ReadOnly = true)]
    // public IReadOnlyList<string> TestListReturn()
    // {
    //     return ["Hello", "World", "MCP", "Tool",];
    // }

    /// <summary>
    /// 测试返回对象中包含集合属性的结构化输出，验证集合属性的 Schema 类型为 array 而非 object
    /// </summary>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public CollectionContainerInfo TestCollectionPropertyReturn()
    {
        return new CollectionContainerInfo
        {
            Tags = ["Hello", "World", "MCP"],
            Items =
            [
                new CollectionItemInfo { Id = 1, Name = "Item1" },
                new CollectionItemInfo { Id = 2, Name = "Item2" },
            ],
        };
    }

    /// <summary>
    /// 测试返回含有 [JsonPropertyName] 特性的结构化输出，验证生成的 Schema 使用自定义属性名
    /// </summary>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public LocalTimeInfoWithCustomNames TestJsonPropertyNameReturn()
    {
        var now = DateTime.Now;
        return new LocalTimeInfoWithCustomNames
        {
            Year = now.Year,
            Month = now.Month,
            Day = now.Day,
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

/// <summary>
/// 用于测试 [JsonPropertyName] 特性的时间信息结构，属性名使用 snake_case 自定义命名
/// </summary>
public record LocalTimeInfoWithCustomNames
{
    /// <summary>
    /// 年份（JSON 键名：year_value）
    /// </summary>
    [JsonPropertyName("year_value")]
    public int Year { get; init; }

    /// <summary>
    /// 月份（JSON 键名：month_value）
    /// </summary>
    [JsonPropertyName("month_value")]
    public int Month { get; init; }

    /// <summary>
    /// 日期（JSON 键名：day_value）
    /// </summary>
    [JsonPropertyName("day_value")]
    public int Day { get; init; }
}

/// <summary>
/// 用于测试对象中包含集合属性的输出结构
/// </summary>
public record CollectionContainerInfo
{
    /// <summary>
    /// 字符串标签列表
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// 子项列表
    /// </summary>
    public IReadOnlyList<CollectionItemInfo>? Items { get; init; }
}

/// <summary>
/// 集合中的子项信息
/// </summary>
public record CollectionItemInfo
{
    /// <summary>
    /// 子项标识符
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// 子项名称
    /// </summary>
    public required string Name { get; init; }
}
