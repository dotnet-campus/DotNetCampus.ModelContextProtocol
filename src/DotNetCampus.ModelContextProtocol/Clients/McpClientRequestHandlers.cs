using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Transports;

namespace DotNetCampus.ModelContextProtocol.Clients;

/// <summary>
/// MCP 客户端请求处理逻辑的基类。<br/>
/// 通过继承此类并重写方法，可以在客户端发送请求前拦截、修改请求参数（如注入 <c>_meta</c>），或对响应做后处理。
/// </summary>
public class McpClientRequestHandlers
{
    private readonly McpClient _client;

    /// <summary>
    /// 初始化 <see cref="McpClientRequestHandlers"/> 的新实例。
    /// </summary>
    public McpClientRequestHandlers(McpClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 获取 MCP 客户端实例。
    /// </summary>
    protected McpClient Client => _client;

    /// <summary>
    /// 获取日志记录器。
    /// </summary>
    protected IMcpLogger Logger => _client.Context.Logger;

    /// <summary>
    /// 获取 MCP 客户端传输层管理器。
    /// </summary>
    private ClientTransportManager Transport => (ClientTransportManager)_client.Context.Transport;

    #region ListTools

    /// <summary>
    /// 发送列出工具请求。重写此方法以实现自定义的请求处理、记录或扩展功能。
    /// </summary>
    /// <param name="requestParams">请求参数，如果没有分页游标则为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工具列表结果。</returns>
    public virtual ValueTask<ListToolsResult> ListToolsAsync(
        ListToolsRequestParams? requestParams, CancellationToken cancellationToken)
    {
        return requestParams is null
            ? SendRequestAsync<ListToolsRequestParams, ListToolsResult>(
                RequestMethods.ToolsList, null,
                McpInternalJsonContext.Default.ListToolsRequestParams, McpInternalJsonContext.Default.ListToolsResult,
                cancellationToken)
            : SendRequestAsync(
                RequestMethods.ToolsList, requestParams,
                McpInternalJsonContext.Default.ListToolsRequestParams, McpInternalJsonContext.Default.ListToolsResult,
                cancellationToken);
    }

    #endregion

    #region CallTool

    /// <summary>
    /// 发送工具调用请求。重写此方法以实现自定义的请求处理、记录或扩展功能。
    /// </summary>
    /// <param name="requestParams">工具调用的请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工具调用结果。</returns>
    public virtual ValueTask<CallToolResult> CallToolAsync(
        CallToolRequestParams requestParams, CancellationToken cancellationToken)
    {
        return SendRequestAsync(
            RequestMethods.ToolsCall, requestParams,
            McpInternalJsonContext.Default.CallToolRequestParams, McpInternalJsonContext.Default.CallToolResult,
            cancellationToken);
    }

    #endregion

    #region ListResources

    /// <summary>
    /// 发送列出资源请求。重写此方法以实现自定义的请求处理、记录或扩展功能。
    /// </summary>
    /// <param name="requestParams">请求参数，如果没有分页游标则为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>资源列表结果。</returns>
    public virtual ValueTask<ListResourcesResult> ListResourcesAsync(
        ListResourcesRequestParams? requestParams, CancellationToken cancellationToken)
    {
        return requestParams is null
            ? SendRequestAsync<ListResourcesRequestParams, ListResourcesResult>(
                RequestMethods.ResourcesList, null,
                McpInternalJsonContext.Default.ListResourcesRequestParams, McpInternalJsonContext.Default.ListResourcesResult,
                cancellationToken)
            : SendRequestAsync(
                RequestMethods.ResourcesList, requestParams,
                McpInternalJsonContext.Default.ListResourcesRequestParams, McpInternalJsonContext.Default.ListResourcesResult,
                cancellationToken);
    }

    #endregion

    #region ReadResource

    /// <summary>
    /// 发送读取资源请求。重写此方法以实现自定义的请求处理、记录或扩展功能。
    /// </summary>
    /// <param name="requestParams">资源读取的请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>资源内容。</returns>
    public virtual ValueTask<ReadResourceResult> ReadResourceAsync(
        ReadResourceRequestParams requestParams, CancellationToken cancellationToken)
    {
        return SendRequestAsync(
            RequestMethods.ResourcesRead, requestParams,
            McpInternalJsonContext.Default.ReadResourceRequestParams, McpInternalJsonContext.Default.ReadResourceResult,
            cancellationToken);
    }

    #endregion

    #region ListPrompts

    /// <summary>
    /// 发送列出提示模板请求。重写此方法以实现自定义的请求处理、记录或扩展功能。
    /// </summary>
    /// <param name="requestParams">请求参数，如果没有分页游标则为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>提示模板列表结果。</returns>
    public virtual ValueTask<ListPromptsResult> ListPromptsAsync(
        ListPromptsRequestParams? requestParams, CancellationToken cancellationToken)
    {
        return requestParams is null
            ? SendRequestAsync<ListPromptsRequestParams, ListPromptsResult>(
                RequestMethods.PromptsList, null,
                McpInternalJsonContext.Default.ListPromptsRequestParams, McpInternalJsonContext.Default.ListPromptsResult,
                cancellationToken)
            : SendRequestAsync(
                RequestMethods.PromptsList, requestParams,
                McpInternalJsonContext.Default.ListPromptsRequestParams, McpInternalJsonContext.Default.ListPromptsResult,
                cancellationToken);
    }

    #endregion

    #region GetPrompt

    /// <summary>
    /// 发送获取提示模板请求。重写此方法以实现自定义的请求处理、记录或扩展功能。
    /// </summary>
    /// <param name="requestParams">获取提示模板的请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>提示内容。</returns>
    public virtual ValueTask<GetPromptResult> GetPromptAsync(
        GetPromptRequestParams requestParams, CancellationToken cancellationToken)
    {
        return SendRequestAsync(
            RequestMethods.PromptsGet, requestParams,
            McpInternalJsonContext.Default.GetPromptRequestParams, McpInternalJsonContext.Default.GetPromptResult,
            cancellationToken);
    }

    #endregion

    #region 全局处理

    /// <summary>
    /// 在请求发送前调用。重写此方法以实现全局的请求预处理，例如注入 <c>_meta</c> 字段。
    /// </summary>
    /// <param name="requestParams">即将发送的请求参数。可以修改其 <see cref="RequestParams.Meta"/> 属性以注入元数据。</param>
    /// <param name="method">JSON-RPC 方法名（如 <c>"tools/call"</c>、<c>"tools/list"</c>）。</param>
    protected internal virtual void OnRequestSending(RequestParams requestParams, string method)
    {
    }

    #endregion

    #region 发送请求辅助方法

    /// <summary>
    /// 发送 JSON-RPC 请求并返回反序列化后的结果。
    /// </summary>
    protected async ValueTask<TResult> SendRequestAsync<TParams, TResult>(
        string method, TParams? requestParams,
        JsonTypeInfo<TParams> paramsTypeInfo, JsonTypeInfo<TResult> resultTypeInfo,
        CancellationToken cancellationToken)
        where TParams : RequestParams
        where TResult : Result
    {
        if (requestParams is not null)
        {
            OnRequestSending(requestParams, method);
        }

        var request = new JsonRpcRequest
        {
            Id = Transport.MakeNewRequestId().ToJsonElement(),
            Method = method,
            Params = requestParams is null
                ? EmptyObject.JsonElement
                : JsonSerializer.SerializeToElement(requestParams, paramsTypeInfo),
        };

        var response = await Transport.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        response.ThrowClientExceptionIfError();
        return DeserializeResult(response, resultTypeInfo);
    }

    /// <summary>
    /// 从 JSON-RPC 响应中反序列化结果。
    /// </summary>
    private static TResult DeserializeResult<TResult>(JsonRpcResponse response, JsonTypeInfo<TResult> jsonTypeInfo) where TResult : Result
    {
        if (response.Error is not null)
        {
            throw new McpClientException($"请求失败: {response.Error.Message}");
        }

        if (response.Result is not { } result)
        {
            throw new McpClientException("响应格式不正确");
        }

        return result.Deserialize(jsonTypeInfo) ?? throw new McpClientException("无法解析响应结果");
    }

    #endregion
}
