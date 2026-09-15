using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Transports;

namespace DotNetCampus.ModelContextProtocol.Clients;

/// <summary>
/// MCP 客户端，用于与 MCP 服务器通信。
/// </summary>
public class McpClient : IAsyncDisposable
{
    private readonly McpClientContext _context;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private InitializeResult? _serverInfo;

    /// <summary>
    /// 初始化 <see cref="McpClient"/> 类的新实例。
    /// </summary>
    /// <param name="context">MCP 客户端的上下文信息。</param>
    internal McpClient(McpClientContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取当前客户端是否已连接到服务器。
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// 获取 MCP 客户端的上下文信息。
    /// </summary>
    internal McpClientContext Context => _context;

    /// <summary>
    /// 获取 MCP 客户端传输层管理器的实现。
    /// </summary>
    private ClientTransportManager Transport => (ClientTransportManager)_context.Transport;

    /// <summary>
    /// 获取请求处理器。
    /// </summary>
    private McpClientRequestHandlers Handlers => _context.Handlers;

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
    /// 启用调试模式。<br/>
    /// 在调试模式下，客户端会记录更多的日志信息以帮助调试。
    /// </summary>
    public void EnableDebugMode()
    {
        _context.IsDebugMode = true;
    }

    /// <summary>
    /// 确保客户端已连接到服务器。如果未连接，则自动连接并完成 MCP 协议初始化握手。
    /// <para>
    /// 此方法是幂等的，多次调用不会重复连接。所有 API 方法（如 <see cref="CallToolAsync"/>）在内部也会自动调用此方法，
    /// 因此通常不需要显式调用。但如果希望在正式使用前提前验证连接可用性（例如过滤掉不可用的服务），可以主动调用此方法。
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 双重检查。
            if (IsConnected)
            {
                return;
            }

            _serverInfo = await Transport.ConnectAndInitializeAsync(this, cancellationToken);

            IsConnected = true;
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

        var requestParams = cursor is null
            ? null
            : new ListToolsRequestParams { Cursor = cursor };

        return await Handlers.ListToolsAsync(requestParams, cancellationToken).ConfigureAwait(false);
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

        var requestParams = new CallToolRequestParams
        {
            Name = toolName,
            Arguments = arguments,
        };

        return await Handlers.CallToolAsync(requestParams, cancellationToken).ConfigureAwait(false);
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

        var requestParams = cursor is null
            ? null
            : new ListResourcesRequestParams { Cursor = cursor };

        return await Handlers.ListResourcesAsync(requestParams, cancellationToken).ConfigureAwait(false);
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

        var requestParams = new ReadResourceRequestParams
        {
            Uri = uri,
        };

        return await Handlers.ReadResourceAsync(requestParams, cancellationToken).ConfigureAwait(false);
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

        var requestParams = cursor is null
            ? null
            : new ListPromptsRequestParams { Cursor = cursor };

        return await Handlers.ListPromptsAsync(requestParams, cancellationToken).ConfigureAwait(false);
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

        var requestParams = new GetPromptRequestParams
        {
            Name = name,
            Arguments = arguments,
        };

        return await Handlers.GetPromptAsync(requestParams, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Transport.DisconnectAsync().ConfigureAwait(false);
        IsConnected = false;
        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
