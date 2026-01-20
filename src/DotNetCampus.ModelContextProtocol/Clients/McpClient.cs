using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Clients;

/// <summary>
/// MCP 客户端，用于与 MCP 服务器通信。
/// </summary>
public class McpClient : IAsyncDisposable
{
    private readonly McpClientContext _context;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private InitializeResult? _serverInfo;
    private long _requestIdCounter;

    /// <summary>
    /// 初始化 <see cref="McpClient"/> 类的新实例。
    /// </summary>
    /// <param name="context">MCP 客户端的上下文信息。</param>
    internal McpClient(McpClientContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取或初始化客户端名称。
    /// </summary>
    public required string ClientName { get; init; }

    /// <summary>
    /// 获取或初始化客户端版本。
    /// </summary>
    public required string ClientVersion { get; init; }

    /// <summary>
    /// 获取客户端能力。
    /// </summary>
    public required ClientCapabilities Capabilities { get; init; }

    /// <summary>
    /// 获取服务器信息（初始化后可用）。
    /// </summary>
    public InitializeResult? ServerInfo => _serverInfo;

    /// <summary>
    /// 获取是否已连接到服务器。
    /// </summary>
    public bool IsConnected => _context.Transport.IsConnected && _serverInfo is not null;

    /// <summary>
    /// 启用调试模式。<br/>
    /// 在调试模式下，客户端会记录更多的日志信息以帮助调试。
    /// </summary>
    public void EnableDebugMode()
    {
        _context.IsDebugMode = true;
    }

    /// <summary>
    /// 确保客户端已连接到服务器。如果未连接，则自动连接并初始化。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 双重检查
            if (IsConnected)
            {
                return;
            }

            // 连接传输层
            if (!_context.Transport.IsConnected)
            {
                await _context.Transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }

            // 发送 initialize 请求
            var request = new JsonRpcRequest
            {
                Id = GenerateRequestId(),
                Method = RequestMethods.Initialize,
                Params = JsonSerializer.SerializeToElement(new InitializeRequestParams
                {
                    ProtocolVersion = ProtocolVersion.Current,
                    ClientInfo = new Implementation
                    {
                        Name = ClientName,
                        Version = ClientVersion,
                    },
                    Capabilities = Capabilities,
                }, McpServerRequestJsonContext.Default.InitializeRequestParams),
            };

            var response = await _context.Transport.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.Error is not null)
            {
                throw new McpClientException($"初始化失败: {response.Error.Message}");
            }

            if (response.Result is not { } responseResult)
            {
                throw new McpClientException("初始化响应格式不正确");
            }

            _serverInfo = responseResult.Deserialize<InitializeResult>(McpServerResponseJsonContext.Default.InitializeResult)
                          ?? throw new McpClientException("无法解析初始化响应");

            // 发送 initialized 通知
            await _context.Transport.SendNotificationAsync(new JsonRpcNotification
            {
                Method = "notifications/initialized",
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// 列出服务器提供的所有工具。
    /// </summary>
    /// <param name="cursor">分页游标（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工具列表结果。</returns>
    public async Task<ListToolsResult> ListToolsAsync(string? cursor = null, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var request = new JsonRpcRequest
        {
            Id = GenerateRequestId(),
            Method = RequestMethods.ToolsList,
            Params = cursor is null
                ? null
                : JsonSerializer.SerializeToElement(new ListToolsRequestParams
                {
                    Cursor = cursor,
                }, McpServerRequestJsonContext.Default.ListToolsRequestParams),
        };

        var response = await _context.Transport.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<ListToolsResult>(response, McpServerResponseJsonContext.Default.ListToolsResult);
    }

    /// <summary>
    /// 调用服务器上的工具。
    /// </summary>
    /// <param name="toolName">工具名称。</param>
    /// <param name="arguments">工具参数（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工具调用结果。</returns>
    public async Task<CallToolResult> CallToolAsync(string toolName, JsonElement? arguments = null, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var request = new JsonRpcRequest
        {
            Id = GenerateRequestId(),
            Method = RequestMethods.ToolsCall,
            Params = JsonSerializer.SerializeToElement(new CallToolRequestParams
            {
                Name = toolName,
                Arguments = arguments,
            }, McpServerRequestJsonContext.Default.CallToolRequestParams),
        };

        var response = await _context.Transport.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<CallToolResult>(response, McpServerResponseJsonContext.Default.CallToolResult);
    }

    /// <summary>
    /// 列出服务器提供的所有资源。
    /// </summary>
    /// <param name="cursor">分页游标（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>资源列表结果。</returns>
    public async Task<ListResourcesResult> ListResourcesAsync(string? cursor = null, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var request = new JsonRpcRequest
        {
            Id = GenerateRequestId(),
            Method = RequestMethods.ResourcesList,
            Params = cursor is null
                ? null
                : JsonSerializer.SerializeToElement(new ListResourcesRequestParams
                {
                    Cursor = cursor,
                }, McpServerRequestJsonContext.Default.ListResourcesRequestParams),
        };

        var response = await _context.Transport.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<ListResourcesResult>(response, McpServerResponseJsonContext.Default.ListResourcesResult);
    }

    /// <summary>
    /// 读取指定 URI 的资源内容。
    /// </summary>
    /// <param name="uri">资源 URI。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>资源内容。</returns>
    public async Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var request = new JsonRpcRequest
        {
            Id = GenerateRequestId(),
            Method = RequestMethods.ResourcesRead,
            Params = JsonSerializer.SerializeToElement(new ReadResourceRequestParams
            {
                Uri = uri,
            }, McpServerRequestJsonContext.Default.ReadResourceRequestParams),
        };

        var response = await _context.Transport.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<ReadResourceResult>(response, McpServerResponseJsonContext.Default.ReadResourceResult);
    }

    /// <summary>
    /// 列出服务器提供的所有提示模板。
    /// </summary>
    /// <param name="cursor">分页游标（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>提示模板列表结果。</returns>
    public async Task<ListPromptsResult> ListPromptsAsync(string? cursor = null, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var request = new JsonRpcRequest
        {
            Id = GenerateRequestId(),
            Method = RequestMethods.PromptsList,
            Params = cursor is null
                ? null
                : JsonSerializer.SerializeToElement(new ListPromptsRequestParams
                {
                    Cursor = cursor,
                }, McpServerRequestJsonContext.Default.ListPromptsRequestParams),
        };

        var response = await _context.Transport.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<ListPromptsResult>(response, McpServerResponseJsonContext.Default.ListPromptsResult);
    }

    /// <summary>
    /// 获取指定名称的提示模板。
    /// </summary>
    /// <param name="name">提示模板名称。</param>
    /// <param name="arguments">模板参数（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>提示内容。</returns>
    public async Task<GetPromptResult> GetPromptAsync(string name, Dictionary<string, string>? arguments = null, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var request = new JsonRpcRequest
        {
            Id = GenerateRequestId(),
            Method = RequestMethods.PromptsGet,
            Params = JsonSerializer.SerializeToElement(new GetPromptRequestParams
            {
                Name = name,
                Arguments = arguments,
            }, McpServerRequestJsonContext.Default.GetPromptRequestParams),
        };

        var response = await _context.Transport.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<GetPromptResult>(response, McpServerResponseJsonContext.Default.GetPromptResult);
    }

    /// <summary>
    /// 生成新的请求 ID。
    /// </summary>
    private JsonElement GenerateRequestId()
    {
        return JsonSerializer.SerializeToElement(Interlocked.Increment(ref _requestIdCounter), CompiledSchemaJsonContext.Default.Int64);
    }

    /// <summary>
    /// 从 JSON-RPC 响应中反序列化结果。
    /// </summary>
    private static T DeserializeResult<T>(JsonRpcResponse response, JsonTypeInfo<T> jsonTypeInfo) where T : Result
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _context.Transport.DisconnectAsync().ConfigureAwait(false);
        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
