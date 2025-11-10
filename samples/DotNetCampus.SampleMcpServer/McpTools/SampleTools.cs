using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol;

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
    public CallToolResult EchoInfo(
        string text,
        EchoOptions options,
        int count = 1,
        EchoExtraData? extraData = null,
        bool isError = false)
    {
        return text;
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
