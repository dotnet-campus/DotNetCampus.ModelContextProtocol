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
/// 提供服务器主动向客户端发起 Sampling（AI 采样）请求的能力。
/// </summary>
public interface IMcpServerSampling
{
    /// <summary>
    /// 指示连接的客户端是否声明了对 Sampling 的支持。<br/>
    /// 在调用 <see cref="CreateMessageAsync"/> 前应检查此属性；若为 <see langword="false"/>，调用将抛出 <see cref="McpSamplingNotSupportedException"/>。
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// 向客户端发送 sampling/createMessage 请求，通过客户端对 LLM 进行采样。
    /// </summary>
    /// <param name="requestParams">采样请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>LLM 生成的采样结果。</returns>
    /// <exception cref="McpSamplingNotSupportedException">当客户端未声明 Sampling 能力时抛出。</exception>
    /// <exception cref="McpSamplingRejectedException">当采样请求被用户（人工审批）拒绝时抛出。</exception>
    Task<CreateMessageResult> CreateMessageAsync(CreateMessageRequestParams requestParams, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IMcpServerSampling"/> 的扩展方法，提供便捷的文本采样接口。
/// </summary>
public static class McpServerSamplingExtensions
{
    /// <summary>
    /// 向客户端发送简单的纯文本采样请求。
    /// </summary>
    /// <param name="sampling">采样服务实例。</param>
    /// <param name="userMessage">用户消息内容。</param>
    /// <param name="maxTokens">最大生成令牌数。</param>
    /// <param name="systemPrompt">可选的系统提示词。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>LLM 生成的采样结果。</returns>
    /// <exception cref="McpSamplingNotSupportedException">当客户端未声明 Sampling 能力时抛出。</exception>
    /// <exception cref="McpSamplingRejectedException">当采样请求被用户拒绝时抛出。</exception>
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
            //
            // 兼容性说明（MCP Inspector 的不规范行为）：
            // MCP Inspector 拒绝采样时，调用的是 reject(new Error("Sampling request rejected"))，
            // 传入的是普通 JavaScript Error，没有 code 属性。
            // TypeScript SDK 在将 handler rejection 转换为 JSON-RPC 错误时，
            // 其逻辑为：code = Number.isSafeInteger(error['code']) ? error['code'] : ErrorCode.InternalError
            // 即当 error 无 code 时回退到 -32603（InternalError），而非规范要求的 -1。
            // 因此需额外检查 -32603 + 消息关键字来识别这类不规范的拒绝响应，
            // 同时避免将真正的服务端内部错误误判为用户拒绝。
            var isRejectedByUser = error.Code > -32000
                || (error.Code == -32603 && error.Message.Contains("reject", StringComparison.OrdinalIgnoreCase));
            if (isRejectedByUser)
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
/// 当传输层或客户端不支持 Sampling 时，用于占位的空对象实现。
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
