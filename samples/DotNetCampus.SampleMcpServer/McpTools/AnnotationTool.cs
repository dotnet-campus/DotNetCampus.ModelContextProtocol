using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.SampleMcpServer.McpTools;

/// <summary>
/// 本 MCP 工具用于测试 McpServerToolAttribute 中各种注解属性的功能。
/// </summary>
public class AnnotationTool
{
    /// <summary>
    /// 只读工具：查询当前时间，不会修改任何状态
    /// </summary>
    [McpServerTool(
        Name = "get_current_time",
        Title = "获取当前时间",
        Description = "返回当前的系统时间",
        ReadOnly = true)]
    public string GetCurrentTime()
    {
        return $"当前时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
    }

    /// <summary>
    /// 幂等工具：设置配置值，多次调用相同参数产生相同结果
    /// </summary>
    [McpServerTool(
        Name = "set_config",
        Title = "设置配置",
        Description = "设置系统配置值（幂等操作）",
        Idempotent = true)]
    public string SetConfig(string key, string value)
    {
        return $"配置已设置：{key} = {value}";
    }

    /// <summary>
    /// 破坏性工具：删除文件，会对环境造成破坏性修改
    /// </summary>
    [McpServerTool(
        Name = "delete_file",
        Title = "删除文件",
        Description = "删除指定的文件（破坏性操作）",
        Destructive = true)]
    public string DeleteFile(string filePath)
    {
        return $"已删除文件：{filePath}（模拟操作）";
    }

    /// <summary>
    /// 开放世界工具：搜索互联网，与外部实体交互
    /// </summary>
    [McpServerTool(
        Name = "search_web",
        Title = "网络搜索",
        Description = "在互联网上搜索信息",
        OpenWorld = true,
        ReadOnly = true)]
    public string SearchWeb(string query)
    {
        return $"搜索结果：'{query}' 的相关信息（模拟结果）";
    }
    //
    // /// <summary>
    // /// 封闭世界工具：内存存储，只与本地状态交互
    // /// </summary>
    // [McpServerTool(
    //     Name = "store_memory",
    //     Title = "存储记忆",
    //     Description = "在本地内存中存储信息",
    //     OpenWorld = false)]
    // public string StoreMemory(string key, string content)
    // {
    //     return $"已存储：{key} -> {content}";
    // }
    //
    // /// <summary>
    // /// 组合属性工具：既是幂等的又是只读的
    // /// </summary>
    // [McpServerTool(
    //     Name = "calculate_hash",
    //     Title = "计算哈希",
    //     Description = "计算字符串的哈希值（只读且幂等）",
    //     ReadOnly = true,
    //     Idempotent = true)]
    // public string CalculateHash(string input)
    // {
    //     var hash = input.GetHashCode();
    //     return $"Hash of '{input}': {hash}";
    // }
    //
    // /// <summary>
    // /// 完整属性工具：演示所有可用属性
    // /// </summary>
    // [McpServerTool(
    //     Name = "full_featured_tool",
    //     Title = "完整功能工具",
    //     Description = "演示所有 ToolAnnotations 属性的工具",
    //     Destructive = false,
    //     Idempotent = true,
    //     OpenWorld = false,
    //     ReadOnly = false)]
    // public string FullFeaturedTool(string operation, string target)
    // {
    //     return $"执行操作：{operation} 在目标：{target}";
    // }
}
