using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Transports;
using DotNetCampus.ModelContextProtocol.Utils;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 提供服务器主动向客户端发起 Sampling（AI 采样）请求的能力。<br/>
/// Provides the server's ability to initiate Sampling (AI sampling) requests to the client.
/// </summary>
public interface IMcpServerSampling
{
    /// <summary>
    /// 指示连接的客户端是否声明了对 Sampling 的支持。<br/>
    /// Indicates whether the connected client has declared support for Sampling.
    /// </summary>
    bool HasSamplingCapability { get; }

    /// <summary>
    /// 向客户端发送 sampling/createMessage 请求，通过客户端对 LLM 进行采样。<br/>
    /// Sends a sampling/createMessage request to the client to sample from an LLM via the client.
    /// </summary>
    /// <param name="requestParams">采样请求参数。Sampling request parameters.</param>
    /// <param name="cancellationToken">取消令牌。Cancellation token.</param>
    /// <returns>LLM 生成的采样结果。The LLM-generated sampling result.</returns>
    /// <exception cref="InvalidOperationException">当客户端未声明 Sampling 能力时抛出。Thrown when the client has not declared Sampling capability.</exception>
    Task<CreateMessageResult> CreateMessageAsync(CreateMessageRequestParams requestParams, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IMcpServerSampling"/> 的扩展方法，提供便捷的文本采样接口。<br/>
/// Extension methods for <see cref="IMcpServerSampling"/>, providing convenient text-only sampling APIs.
/// </summary>
public static class McpServerSamplingExtensions
{
    /// <summary>
    /// 向客户端发送简单的纯文本采样请求。<br/>
    /// Sends a simple plain-text sampling request to the client.
    /// </summary>
    /// <param name="sampling">采样服务实例。Sampling service instance.</param>
    /// <param name="userMessage">用户消息内容。User message content.</param>
    /// <param name="maxTokens">最大生成令牌数。Maximum number of tokens to generate.</param>
    /// <param name="systemPrompt">可选的系统提示词。Optional system prompt.</param>
    /// <param name="cancellationToken">取消令牌。Cancellation token.</param>
    /// <returns>LLM 生成的采样结果。The LLM-generated sampling result.</returns>
    public static Task<CreateMessageResult> CreateMessageAsync(
        this IMcpServerSampling sampling,
        string userMessage,
        int maxTokens = 1024,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var requestParams = new CreateMessageRequestParams
        {
            Messages =
            [
                new SamplingMessage
                {
                    Role = Role.User,
                    Content = new TextContentBlock { Text = userMessage },
                },
            ],
            MaxTokens = maxTokens,
            SystemPrompt = systemPrompt,
        };
        return sampling.CreateMessageAsync(requestParams, cancellationToken);
    }
}

/// <summary>
/// <see cref="IMcpServerSampling"/> 的内部实现，通过关联的传输层会话与客户端通信。
/// </summary>
internal sealed class McpServerSampling(IServerTransportSession session) : IMcpServerSampling
{
    /// <inheritdoc />
    public bool HasSamplingCapability => session.ConnectedClientCapabilities?.Sampling is not null;

    /// <inheritdoc />
    public async Task<CreateMessageResult> CreateMessageAsync(
        CreateMessageRequestParams requestParams,
        CancellationToken cancellationToken = default)
    {
        if (!HasSamplingCapability)
        {
            throw new InvalidOperationException("连接的客户端未声明对 Sampling 的支持。The connected client has not declared Sampling capability.");
        }

        var request = new JsonRpcRequest
        {
            Id = RequestId.MakeNew().ToJsonElement(),
            Method = RequestMethods.SamplingCreateMessage,
            Params = JsonSerializer.SerializeToElement(requestParams, McpServerRequestJsonContext.Default.CreateMessageRequestParams),
        };

        var response = await session.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.Error is { } error)
        {
            throw new McpClientException($"Sampling request failed: [{error.Code}] {error.Message}");
        }

        if (response.Result is not { } resultElement)
        {
            throw new McpClientException("Sampling response missing result.");
        }

        return resultElement.Deserialize(McpServerResponseJsonContext.Default.CreateMessageResult)
               ?? throw new McpClientException("Failed to deserialize sampling result.");
    }
}
