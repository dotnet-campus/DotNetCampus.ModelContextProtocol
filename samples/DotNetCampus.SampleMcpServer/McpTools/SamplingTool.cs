using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.SampleMcpServer.McpTools;

public class SamplingTool
{
    /// <summary>
    /// 通过客户端的 LLM 进行采样，将 prompt 发送给客户端，获取 LLM 响应并返回。
    /// 用于人工验证 sampling/createMessage 协议流程是否正常。
    /// </summary>
    /// <param name="prompt">发送给 LLM 的提示词</param>
    /// <param name="maxTokens">最大生成令牌数</param>
    /// <param name="systemPrompt">可选的系统提示词</param>
    /// <param name="context">MCP 工具上下文</param>
    [McpServerTool]
    public async Task<CallToolResult> AskLlm(
        string prompt,
        int maxTokens = 1024,
        string? systemPrompt = null,
        IMcpServerCallToolContext context = null!)
    {
        if (!context.Sampling.HasSamplingCapability)
        {
            return CallToolResult.FromError(
                "当前客户端未声明 Sampling 能力。请确保客户端支持 sampling/createMessage 请求。\n" +
                "The connected client has not declared Sampling capability.");
        }

        try
        {
            var result = await context.Sampling.CreateMessageAsync(prompt, maxTokens, systemPrompt, context.CancellationToken);

            var responseText = result.Content switch
            {
                TextContentBlock text => text.Text,
                _ => $"[Non-text content: {result.Content?.GetType().Name}]",
            };

            return $"""
                Model: {result.Model}
                StopReason: {result.StopReason ?? "unknown"}
                Role: {result.Role}
                ---
                {responseText}
                """;
        }
        catch (McpSamplingRejectedException ex)
        {
            return CallToolResult.FromError(
                $"采样请求被用户拒绝。Sampling request was rejected by the user.\n" +
                $"Code: {ex.ErrorCode}, Message: {ex.RejectionMessage}");
        }
    }
}
