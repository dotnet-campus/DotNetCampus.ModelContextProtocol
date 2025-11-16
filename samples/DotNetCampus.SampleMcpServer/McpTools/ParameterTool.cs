using System.Text.Json;
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
    /// 演示使用上下文参数的工具
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
    /// 演示使用 InputObject 参数的工具（接收整个 JSON 对象）
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
    /// 演示使用注入参数的工具
    /// </summary>
    /// <param name="logger1">注入的日志记录器</param>
    /// <param name="logger2">可空注入的日志记录器</param>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public string TestParameterInjection(
        [ToolParameter(Type = ToolParameterType.Injected)]
        ILogger logger1,
        [ToolParameter(Type = ToolParameterType.Injected)]
        ILogger? logger2)
    {
        logger1.Info($"[ParameterTool] injected logger works! logger2 is {(logger2 == null ? "null" : "not null")}");
        return "Logger 注入成功！";
    }

    /// <summary>
    /// 演示使用传递任意 JSON 元素参数的工具
    /// </summary>
    /// <param name="id">传递过来的标识</param>
    /// <param name="payload">
    /// 传递过来的任意 JSON 元素（可以是字符串、数字、布尔值、对象、数组或 null）。
    /// 使用 payload.ValueKind 判断实际类型，然后使用对应的 Get* 方法获取值。
    /// </param>
    /// <returns></returns>
    /// <remarks>
    /// 运行时，payload 参数将接收到 <see cref="JsonElement"/> 类型的值。
    /// 可以使用 <see cref="JsonElement.ValueKind"/> 判断实际类型，
    /// 然后调用相应的方法（如 GetString()、GetInt32()、EnumerateObject() 等）。
    /// </remarks>
    // [McpServerTool(ReadOnly = true)] // Visual Studio Code Copilot 不支持此参数，会导致所有工具不可用。
    public string TestParameterAny(string id, object payload)
    {
        return $"""
            Received JSON element, "{id}" = {payload}
            """;
    }

    /// <summary>
    /// 演示使用 JsonElement 参数的工具（直接接收 JSON 元素）
    /// </summary>
    /// <param name="id">传递过来的标识</param>
    /// <param name="data">传递过来的 JSON 元素</param>
    /// <returns></returns>
    // [McpServerTool(ReadOnly = true)] // Visual Studio Code Copilot 不支持此参数，会导致所有工具不可用。
    public string TestParameterJsonElement(string id, JsonElement data)
    {
        var typeInfo = data.ValueKind switch
        {
            JsonValueKind.String => $"String: {data.GetString()}",
            JsonValueKind.Number => $"Number: {data.GetDouble()}",
            JsonValueKind.True or JsonValueKind.False => $"Boolean: {data.GetBoolean()}",
            JsonValueKind.Object => $"Object with {data.EnumerateObject().Count()} properties",
            JsonValueKind.Array => $"Array with {data.GetArrayLength()} items",
            JsonValueKind.Null => "Null",
            _ => "Undefined"
        };

        return $"""
            Received "{id}": {typeInfo}
            Raw JSON: {data.GetRawText()}
            """;
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
