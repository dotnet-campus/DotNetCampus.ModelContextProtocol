using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
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
    /// 在调用 <see cref="CreateMessageAsync"/> 前应检查此属性；若为 <see langword="false"/>，调用将抛出 <see cref="McpSamplingNotSupportedException"/>。<br/>
    /// Indicates whether the connected client has declared support for Sampling.
    /// Check this property before calling <see cref="CreateMessageAsync"/>; if false, the call will throw <see cref="McpSamplingNotSupportedException"/>.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// 向客户端发送 sampling/createMessage 请求，通过客户端对 LLM 进行采样。<br/>
    /// Sends a sampling/createMessage request to the client to sample from an LLM via the client.
    /// </summary>
    /// <param name="requestParams">采样请求参数。Sampling request parameters.</param>
    /// <param name="cancellationToken">取消令牌。Cancellation token.</param>
    /// <returns>LLM 生成的采样结果。The LLM-generated sampling result.</returns>
    /// <exception cref="McpSamplingNotSupportedException">当客户端未声明 Sampling 能力时抛出。Thrown when the client has not declared Sampling capability.</exception>
    /// <exception cref="McpSamplingRejectedException">当采样请求被用户（人工审批）拒绝时抛出。Thrown when the sampling request was rejected by the user (human-in-the-loop).</exception>
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
    /// <exception cref="McpSamplingNotSupportedException">当客户端未声明 Sampling 能力时抛出。Thrown when the client has not declared Sampling capability.</exception>
    /// <exception cref="McpSamplingRejectedException">当采样请求被用户拒绝时抛出。Thrown when the sampling request was rejected by the user.</exception>
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
internal sealed class McpServerSampling(IServerTransportSession session, IMcpLogger logger) : IMcpServerSampling
{
    /// <inheritdoc />
    public bool IsSupported => session.ConnectedClientCapabilities?.Sampling is not null;

    /// <inheritdoc />
    public async Task<CreateMessageResult> CreateMessageAsync(
        CreateMessageRequestParams requestParams,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
        {
            throw new McpSamplingNotSupportedException();
        }

        var request = new JsonRpcRequest
        {
            Id = RequestId.MakeNew().ToJsonElement(),
            Method = RequestMethods.SamplingCreateMessage,
            Params = JsonSerializer.SerializeToElement(requestParams, McpInternalJsonContext.Default.CreateMessageRequestParams),
        };

        logger.Debug($"[McpServer][Mcp] Sending sampling/createMessage request. Id={request.Id}");

        var response = await session.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.Error is { } error)
        {
            // 根据 MCP 规范，用户拒绝审批时客户端应返回错误响应。
            // JSON-RPC 保留错误码范围为 -32768 到 -32000；任何高于 -32000 的错误码（如 -1）
            // 表示用户自定义错误，通常意味着用户主动拒绝了采样请求。
            // Per the MCP spec, when a user denies a sampling request, the client returns an error response.
            // JSON-RPC reserved error codes are in range -32768 to -32000; any code above -32000 (e.g., -1)
            // is user-defined and typically indicates an explicit rejection by the human-in-the-loop.
            if (error.Code > -32000)
            {
                logger.Warn($"[McpServer][Mcp] Sampling/createMessage rejected by user. Id={request.Id}, Code={error.Code}, Message={error.Message}");
                throw new McpSamplingRejectedException(error.Code, error.Message);
            }

            logger.Error($"[McpServer][Mcp] Sampling/createMessage failed. Id={request.Id}, Code={error.Code}, Message={error.Message}");
            throw new McpClientException($"Sampling request failed: [{error.Code}] {error.Message}");
        }

        if (response.Result is not { } resultElement)
        {
            throw new McpClientException("Sampling response missing result.");
        }

        logger.Debug($"[McpServer][Mcp] Sampling/createMessage succeeded. Id={request.Id}");

        return resultElement.Deserialize(McpInternalJsonContext.Default.CreateMessageResult)
               ?? throw new McpClientException("Failed to deserialize sampling result.");
    }
}

/// <summary>
/// 当传输层或客户端不支持 Sampling 时，用于占位的空对象实现。<br/>
/// Null-object implementation of <see cref="IMcpServerSampling"/> used when the transport or client does not support Sampling.
/// </summary>
internal sealed class NotSupportedMcpServerSampling : IMcpServerSampling
{
    /// <summary>
    /// 获取全局单例实例。
    /// </summary>
    public static readonly NotSupportedMcpServerSampling Instance = new();

    private NotSupportedMcpServerSampling() { }

    /// <inheritdoc />
    public bool IsSupported => false;

    /// <inheritdoc />
    public Task<CreateMessageResult> CreateMessageAsync(CreateMessageRequestParams requestParams, CancellationToken cancellationToken = default)
        => throw new McpSamplingNotSupportedException();
}
