using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.SampleMcpServer.McpTools;

/// <summary>
/// 本 MCP 工具用于测试各种不同类型的输出功能。
/// </summary>
public class OutputTool
{
    #region 无返回值（void / Task / ValueTask）

    /// <summary>
    /// 测试同步无返回值（void）
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public void ReturnVoid()
    {
    }

    /// <summary>
    /// 测试异步无返回值（Task）
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public async Task ReturnTask()
    {
        await Task.Yield();
    }

    /// <summary>
    /// 测试异步无返回值（ValueTask）
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public async ValueTask ReturnValueTask()
    {
        await Task.Yield();
    }

    #endregion

    #region 字符串返回

    /// <summary>
    /// 测试返回字符串
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public string ReturnString()
    {
        return "Hello, MCP!";
    }

    /// <summary>
    /// 测试返回可空字符串
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public async Task<string?> ReturnNullableString()
    {
        return null;
    }

    #endregion

    #region 基本类型返回（ToString 输出）

    /// <summary>
    /// 测试返回 int
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public int ReturnInt()
    {
        return 42;
    }

    /// <summary>
    /// 测试返回 bool
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public bool ReturnBool()
    {
        return true;
    }

    /// <summary>
    /// 测试返回 double
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public double ReturnDouble()
    {
        return 3.14;
    }

    /// <summary>
    /// 测试返回枚举值
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public DayOfWeek ReturnEnum()
    {
        return DayOfWeek.Monday;
    }

    #endregion

    #region 基本类型集合返回（每个元素生成一个 TextContentBlock）

    /// <summary>
    /// 测试返回字符串集合
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public IReadOnlyList<string> ReturnStringList()
    {
        return ["Hello", "World", "MCP", "Tool"];
    }

    /// <summary>
    /// 测试返回 int 数组
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public int[] ReturnIntArray()
    {
        return [1, 2, 3, 4, 5];
    }

    /// <summary>
    /// 测试返回枚举集合
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public IReadOnlyList<DayOfWeek> ReturnEnumList()
    {
        return [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];
    }

    #endregion

    #region 结构化对象返回（默认启用 OutputSchema）

    /// <summary>
    /// 测试同步返回结构化对象
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public LocalTimeInfo ReturnObject()
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
    /// 测试异步返回结构化对象
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public async Task<LocalTimeInfo> ReturnObjectAsync()
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
    /// 测试返回含集合属性的结构化对象
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public CollectionContainerInfo ReturnObjectWithCollectionProperties()
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
    /// 测试返回含 [JsonPropertyName] 特性的结构化对象
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public LocalTimeInfoWithCustomNames ReturnObjectWithJsonPropertyName()
    {
        var now = DateTime.Now;
        return new LocalTimeInfoWithCustomNames
        {
            Year = now.Year,
            Month = now.Month,
            Day = now.Day,
        };
    }

    #endregion

    #region 非结构化对象返回（Structured=false）

    /// <summary>
    /// 测试非空对象返回，通过 Structured=false 禁用结构化
    /// </summary>
    [McpServerTool(ReadOnly = true, Structured = false)]
    public LocalTimeInfo ReturnObjectUnstructured()
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
    /// 测试可空对象返回，必须显式设置 Structured=false
    /// </summary>
    [McpServerTool(ReadOnly = true, Structured = false)]
    public LocalTimeInfo? ReturnNullableObject()
    {
        return null;
    }

    /// <summary>
    /// 测试对象集合返回，必须显式设置 Structured=false
    /// </summary>
    [McpServerTool(ReadOnly = true, Structured = false)]
    public IReadOnlyList<LocalTimeInfo> ReturnObjectList()
    {
        return
        [
            new LocalTimeInfo { Year = 2026, Month = 1, Day = 1, Hour = 0, Minute = 0, Second = 0 },
            new LocalTimeInfo { Year = 2026, Month = 5, Day = 29, Hour = 10, Minute = 0, Second = 0 },
        ];
    }

    #endregion

    #region CallToolResult 直接返回

    /// <summary>
    /// 测试同步直接返回 CallToolResult
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public CallToolResult ReturnCallToolResult()
    {
        return CallToolResult.Empty;
    }

    /// <summary>
    /// 测试异步直接返回 CallToolResult
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public async Task<CallToolResult> ReturnCallToolResultAsync()
    {
        await Task.Yield();
        return CallToolResult.Empty;
    }

    #endregion

    #region 编译错误情况（解除注释可验证诊断）

    // --- DM0101: 对不可结构化的类型设置 Structured=true ---

    // 解除注释将报告编译错误 DM0101：string 不可结构化，不允许设置 Structured=true
    // [McpServerTool(ReadOnly = true, Structured = true)]
    // public string ReturnStringStructuredTrue()
    // {
    //     return "Hello";
    // }

    // 解除注释将报告编译错误 DM0101：void 不可结构化，不允许设置 Structured=true
    // [McpServerTool(ReadOnly = true, Structured = true)]
    // public void ReturnVoidStructuredTrue()
    // {
    // }

    // 解除注释将报告编译错误 DM0101：int 不可结构化，不允许设置 Structured=true
    // [McpServerTool(ReadOnly = true, Structured = true)]
    // public int ReturnIntStructuredTrue()
    // {
    //     return 42;
    // }

    // 解除注释将报告编译错误 DM0101：CallToolResult 不可结构化，不允许设置 Structured=true
    // [McpServerTool(ReadOnly = true, Structured = true)]
    // public CallToolResult ReturnCallToolResultStructuredTrue()
    // {
    //     return CallToolResult.Empty;
    // }

    // 解除注释将报告编译错误 DM0101：基本类型集合不可结构化，不允许设置 Structured=true
    // [McpServerTool(ReadOnly = true, Structured = true)]
    // public IReadOnlyList<string> ReturnStringListStructuredTrue()
    // {
    //     return ["Hello"];
    // }

    // --- DM0102: 可空对象或对象集合未显式设置 Structured ---

    // 解除注释将报告编译错误 DM0102：可空对象返回必须显式设置 Structured
    // [McpServerTool(ReadOnly = true)]
    // public LocalTimeInfo? ReturnNullableObjectWithoutStructured()
    // {
    //     return null;
    // }

    // 解除注释将报告编译错误 DM0102：对象集合返回必须显式设置 Structured
    // [McpServerTool(ReadOnly = true)]
    // public IReadOnlyList<LocalTimeInfo> ReturnObjectListWithoutStructured()
    // {
    //     return [];
    // }

    // --- DM0103: 可空对象或对象集合设置 Structured=true ---

    // 解除注释将报告编译错误 DM0103：可空对象不允许 Structured=true（MCP 协议 outputSchema 不支持 null）
    // [McpServerTool(ReadOnly = true, Structured = true)]
    // public LocalTimeInfo? ReturnNullableObjectStructuredTrue()
    // {
    //     return null;
    // }

    // 解除注释将报告编译错误 DM0103：对象集合不允许 Structured=true（MCP 协议 outputSchema.type 必须是 "object"）
    // [McpServerTool(ReadOnly = true, Structured = true)]
    // public IReadOnlyList<LocalTimeInfo> ReturnObjectListStructuredTrue()
    // {
    //     return [];
    // }

    #endregion
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
