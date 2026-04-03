using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.Tests.McpTools;

/// <summary>
/// 用于测试服务器向客户端发起 Sampling 请求的工具。
/// </summary>
public class SamplingTool
{
    /// <summary>
    /// 通过服务端 Sampling 能力向客户端 LLM 发起采样请求，并返回结果文本。
    /// </summary>
    [McpServerTool]
    public async Task<string> AskLlm(string message, IMcpServerCallToolContext context)
    {
        if (!context.Sampling.HasSamplingCapability)
        {
            throw new InvalidOperationException("Sampling service not available in this context.");
        }

        var result = await context.Sampling.CreateMessageAsync(message);
        return result.Content is TextContentBlock textBlock ? textBlock.Text : string.Empty;
    }

    /// <summary>
    /// 检查客户端是否声明了 Sampling 能力（HasSamplingCapability）。
    /// </summary>
    [McpServerTool]
    public string CheckSamplingCapability(IMcpServerCallToolContext context)
    {
        return $"has_capability={context.Sampling.HasSamplingCapability}";
    }
}
