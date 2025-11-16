using DotNetCampus.Logging;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.SampleMcpServer.McpTools;

/// <summary>
/// 本 MCP 工具用于测试各种不同类型的参数传递功能。
/// </summary>
public class ParameterTool
{
    /// <summary>
    /// [ForAI] 演示使用上下文参数的工具
    /// </summary>
    /// <param name="context">传递过来的 MCP 工具调用上下文</param>
    /// <param name="message">工具会原样返回这个字符串</param>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public string TestParameterContext(IMcpServerCallToolContext context, string message)
    {
        return $"""
            Received message: {message},
            Server name: {context.McpServer.ServerName},
            Services: {context.Services},
            InputJsonArguments: {context.InputJsonArguments},
            """;
    }

    /// <summary>
    /// [ForAI] 演示使用 InputObject 参数的工具（接收整个 JSON 对象）
    /// </summary>
    /// <param name="input">整个工具调用的输入对象</param>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public string TestParameterInputObject(
        [ToolParameter(Type = ToolParameterType.InputObject)]
        SampleInputObject input)
    {
        return $"""
            接收到输入对象:
            文本: {input.Text}
            次数: {input.Count}
            """;
    }

    /// <summary>
    /// [ForAI] 演示使用注入参数的工具
    /// </summary>
    /// <param name="logger">注入的日志记录器</param>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public string TestParameterInjection(
        [ToolParameter(Type = ToolParameterType.Injected)]
        ILogger logger)
    {
        logger.Info("[ParameterTool] injected logger works!");
        return "Logger 注入成功！";
    }
}

/// <summary>
/// 示例整个输入对象
/// </summary>
/// <param name="Text">要处理的文本</param>
public record SampleInputObject(string Text)
{
    /// <summary>
    /// 重复次数
    /// </summary>
    public int Count { get; init; } = 1;
}
