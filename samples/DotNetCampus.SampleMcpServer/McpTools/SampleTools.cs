using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.SampleMcpServer.McpTools;

public class SampleTools
{
    /// <summary>
    /// 用于给 AI 调试使用的工具，原样返回一些信息
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
        var result = $"""
            Echoing text: {text}
            Options: {options}
            Count: {count}
            ExtraData: {extraData}
            """;
        return isError
            ? CallToolResult.FromError(result)
            : result;
    }

    /// <summary>
    /// 等待一小段时间（不太精确，AI 感觉需要的时候可以使用）
    /// </summary>
    /// <param name="minutes">等待的分钟数</param>
    /// <param name="seconds">等待的秒数</param>
    /// <param name="milliseconds">等待的毫秒数</param>
    /// <param name="description">等待的描述信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [McpServerTool]
    public async Task<string> Delay(
        int minutes = 0,
        int seconds = 0,
        int milliseconds = 0,
        string? description = null,
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
            return description ?? "Delay completed.";
        }
        catch (OperationCanceledException)
        {
            return "Delay was canceled.";
        }
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
