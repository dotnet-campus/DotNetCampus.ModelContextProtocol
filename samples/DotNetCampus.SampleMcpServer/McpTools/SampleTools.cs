using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.SampleMcpServer.McpTools;

public class SampleTools
{
    /// <summary>
    /// [ForAI] 用于给 AI 调试使用的工具，原样返回一些信息
    /// </summary>
    /// <param name="text">要原样返回的字符串</param>
    /// <param name="options">如何返回字符串</param>
    /// <param name="count">要返回的字符串次数</param>
    /// <param name="extraData">无意义的额外信息</param>
    /// <param name="isError">如果希望工具直接报告错误，则传入 true</param>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public CallToolResult Echo(
        string text,
        EchoOptions options,
        int count = 1,
        EchoExtraData? extraData = null,
        bool isError = false)
    {
        return text;
    }

    /// <summary>
    /// [ForAI] 等待一小段时间（不太精确，AI 感觉需要的时候可以使用）
    /// </summary>
    /// <param name="minutes">等待的分钟数</param>
    /// <param name="seconds">等待的秒数</param>
    /// <param name="milliseconds">等待的毫秒数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [McpServerTool]
    public async Task<string> Delay(
        int minutes = 0,
        int seconds = 0,
        int milliseconds = 0,
        CancellationToken cancellationToken = default)
    {
        var totalMilliseconds = (minutes * 60 + seconds) * 1000 + milliseconds;
        if (totalMilliseconds <= 0)
        {
            return "No delay requested.";
        }
        try
        {
            await Task.Delay(totalMilliseconds, cancellationToken);
            return "Delay completed.";
        }
        catch (OperationCanceledException)
        {
            return "Delay was canceled.";
        }
    }

    /// <summary>
    /// [ForAI] 演示使用 IMcpServerCallToolContext 参数的工具
    /// </summary>
    /// <param name="message">要处理的消息</param>
    /// <param name="context">MCP 服务器工具调用上下文</param>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public CallToolResult ProcessWithContext(
        string message,
        IMcpServerCallToolContext context)
    {
        // 可以从 context 访问各种上下文信息
        var hasServices = context.Services != null;
        var hasJsonContext = context.JsonSerializerContext != null;
        
        return new CallToolResult
        {
            Content = [new TextContentBlock
            {
                Text = $"消息: {message}\n" +
                       $"服务提供者可用: {hasServices}\n" +
                       $"JSON 序列化上下文可用: {hasJsonContext}"
            }]
        };
    }

    /// <summary>
    /// [ForAI] 演示使用 InputObject 参数的工具（接收整个 JSON 对象）
    /// </summary>
    /// <param name="input">整个工具调用的输入对象</param>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public CallToolResult ProcessInputObject(
        [ToolParameter(Type = ToolParameterType.InputObject)]
        EchoInputObject input)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock
            {
                Text = $"接收到输入对象:\n" +
                       $"文本: {input.Text}\n" +
                       $"次数: {input.Count}\n" +
                       $"选项: {input.Options}"
            }]
        };
    }
}

/// <summary>
/// 如何返回字符串。
/// </summary>
public enum EchoOptions
{
    /// <summary>
    /// 以纯文本形式返回。
    /// </summary>
    PlainText,

    /// <summary>
    /// 以 JSON 对象形式返回。
    /// </summary>
    JsonObject,
}

/// <summary>
/// 无意义的额外信息
/// </summary>
/// <param name="Data1">可供保存的第 1 个值</param>
public record EchoExtraData(string Data1)
{
    /// <summary>
    /// 可供保存的第 2 个值
    /// </summary>
    public string Data2 { get; init; } = "";
}

/// <summary>
/// InputObject 类型的输入对象示例
/// </summary>
/// <param name="Text">要处理的文本</param>
/// <param name="Options">如何处理文本</param>
public record EchoInputObject(string Text, EchoOptions Options)
{
    /// <summary>
    /// 重复次数
    /// </summary>
    public int Count { get; init; } = 1;
}
