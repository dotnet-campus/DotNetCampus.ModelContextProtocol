using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Transports;
using DotNetCampus.ModelContextProtocol.Transports.Http;
using DotNetCampus.ModelContextProtocol.Transports.Stdio;
using DotNetCampus.ModelContextProtocol.Utils;

namespace DotNetCampus.ModelContextProtocol.Clients;

/// <summary>
/// 用于构建 MCP 客户端的生成器。
/// </summary>
public class McpClientBuilder
{
    private string _clientName = "<Unknown>";
    private string _clientVersion = "0.0.0";
    private IMcpLogger? _logger;
    private McpTransportRawMessageLoggingDetailLevel _rawMessageLoggingDetailLevel = McpTransportRawMessageLoggingDetailLevel.None;
    private IServiceProvider? _serviceProvider;
    private Func<IClientTransportManager, IClientTransport>? _transportFactory;
    private ClientCapabilities _capabilities = new();
    private Func<CreateMessageRequestParams, CancellationToken, Task<CreateMessageResult>>? _samplingHandler;
    private string _preferredProtocolVersion = ProtocolVersion.Current;
    private IReadOnlyList<string> _supportedProtocolVersions = ProtocolVersion.StreamableHttpSupportedVersions;

    /// <summary>
    /// 设置客户端名称和版本。
    /// </summary>
    /// <param name="clientName">客户端名称。</param>
    /// <param name="clientVersion">客户端版本。</param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithClientInfo(string clientName, string clientVersion)
    {
        _clientName = clientName;
        _clientVersion = clientVersion;
        return this;
    }

    /// <summary>
    /// 配置 MCP 客户端的日志记录器。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithLogger(IMcpLogger logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// 配置 MCP 客户端的日志记录器。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="rawMessageLoggingDetailLevel">传输层原始消息的日志记录详细级别。</param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithLogger(IMcpLogger logger, McpTransportRawMessageLoggingDetailLevel rawMessageLoggingDetailLevel)
    {
        _logger = logger;
        _rawMessageLoggingDetailLevel = rawMessageLoggingDetailLevel;
        return this;
    }

    /// <summary>
    /// 配置 MCP 客户端的服务提供器。
    /// </summary>
    /// <param name="services">服务提供器。</param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithServices(IServiceProvider services)
    {
        _serviceProvider = services;
        return this;
    }

    /// <summary>
    /// 使用 STDIO 传输层连接到 MCP 服务器。
    /// </summary>
    /// <param name="command">要启动的命令。</param>
    /// <param name="arguments">命令的启动参数。</param>
    /// <param name="environmentVariables">命令执行的环境变量。</param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithStdio(string command, IReadOnlyList<string>? arguments = null, IDictionary<string, string>? environmentVariables = null)
    {
        return WithTransport(m => new StdioClientTransport(m, new StdioClientTransportOptions
        {
            Command = command,
            Arguments = arguments ?? [],
            EnvironmentVariables = environmentVariables ?? new Dictionary<string, string>(),
        }));
    }

    /// <summary>
    /// 使用 STDIO 传输层连接到 MCP 服务器。
    /// </summary>
    /// <param name="options">STDIO 传输层的连接信息。</param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithStdio(StdioClientTransportOptions options)
    {
        return WithTransport(m => new StdioClientTransport(m, options));
    }

    /// <summary>
    /// 使用 Streamable HTTP 传输层连接到 MCP 服务器。
    /// </summary>
    /// <param name="serverUrl">要连接的 MCP 服务器的 URL。</param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithHttp(string serverUrl)
    {
        return WithHttp(new HttpClientTransportOptions
        {
            ServerUrl = serverUrl,
        });
    }

    /// <summary>
    /// 使用 Streamable HTTP 传输层连接到 MCP 服务器。
    /// </summary>
    /// <param name="options">Streamable HTTP 传输层的连接信息。</param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithHttp(HttpClientTransportOptions options)
    {
        _supportedProtocolVersions = NormalizeSupportedProtocolVersions(options.SupportedProtocolVersions);
        _preferredProtocolVersion = NormalizePreferredProtocolVersion(options.PreferredProtocolVersion);
        ValidateProtocolVersionConfiguration(_preferredProtocolVersion, _supportedProtocolVersions);
        return WithTransport(m => new HttpClientTransport(m, options));
    }

    /// <summary>
    /// 使用自定义的传输层。
    /// </summary>
    /// <param name="transportFactory">传输层工厂方法。</param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithTransport(Func<IClientTransportManager, IClientTransport> transportFactory)
    {
        if (_transportFactory is not null)
        {
            throw new InvalidOperationException("MCP 客户端在设计上只能通过一个传输层对接一个服务器。");
        }

        _transportFactory = transportFactory;
        return this;
    }

    /// <summary>
    /// 配置客户端能力。
    /// </summary>
    /// <param name="capabilities">客户端能力。</param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithCapabilities(ClientCapabilities capabilities)
    {
        _capabilities = capabilities;
        return this;
    }

    /// <summary>
    /// 配置 Sampling 处理器，使客户端支持服务器发起的 sampling/createMessage 请求。
    /// 调用此方法会自动在客户端能力中声明 Sampling 支持。
    /// </summary>
    /// <param name="handler">
    /// 当服务器请求采样时的处理函数。接收 <see cref="CreateMessageRequestParams"/> 并返回 <see cref="CreateMessageResult"/>。
    /// </param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithSamplingHandler(
        Func<CreateMessageRequestParams, CancellationToken, Task<CreateMessageResult>> handler)
    {
        _samplingHandler = handler;
        _capabilities = _capabilities with
        {
            Sampling = _capabilities.Sampling ?? new SamplingCapability(),
        };
        return this;
    }

    /// <summary>
    /// 配置 Sampling 处理器，使客户端支持服务器发起的 sampling/createMessage 请求。
    /// 调用此方法会自动在客户端能力中声明 Sampling 支持。
    /// </summary>
    /// <param name="handlerFactory">
    /// 处理函数工厂，接收 <see cref="IServiceProvider"/> 以便从中获取所需服务。
    /// </param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithSamplingHandler(
        Func<IServiceProvider?, Func<CreateMessageRequestParams, CancellationToken, Task<CreateMessageResult>>> handlerFactory)
    {
        return WithSamplingHandler((p, ct) =>
        {
            var handler = handlerFactory(_serviceProvider);
            return handler(p, ct);
        });
    }

    /// <summary>
    /// 构建 MCP 客户端实例。
    /// </summary>
    /// <returns>构建好的 MCP 客户端。</returns>
    public McpClient Build()
    {
        if (_transportFactory is null)
        {
            throw new InvalidOperationException("必须配置传输层。请调用 WithTransport 或相关的传输层配置方法。");
        }

        var context = new McpClientContext
        {
            Logger = _logger ?? EmptyLogger.Instance,
            ServiceProvider = _serviceProvider,
        };

        var transportManager = new ClientTransportManager(context)
        {
            RawMessageLoggingDetailLevel = _rawMessageLoggingDetailLevel,
        };
        context.Transport = transportManager;

        if (_samplingHandler is { } handler)
        {
            transportManager.SetSamplingHandler(handler);
        }

        var transport = _transportFactory(transportManager);
        transportManager.SetTransport(transport);

        return new McpClient(context)
        {
            ClientName = _clientName,
            ClientVersion = _clientVersion,
            Capabilities = _capabilities,
            PreferredProtocolVersion = _preferredProtocolVersion,
            SupportedProtocolVersions = _supportedProtocolVersions,
        };
    }

    private static IReadOnlyList<string> NormalizeSupportedProtocolVersions(IReadOnlyList<string>? supportedProtocolVersions)
    {
        if (supportedProtocolVersions is null || supportedProtocolVersions.Count == 0)
        {
            return ProtocolVersion.StreamableHttpSupportedVersions;
        }

        var normalizedVersions = supportedProtocolVersions
            .Where(static version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedVersions.Length == 0)
        {
            throw new InvalidOperationException("至少需要配置一个可接受的协议版本。");
        }

        foreach (var version in normalizedVersions)
        {
            if (!ProtocolVersion.IsSupportedStreamableHttpVersion(version))
            {
                throw new InvalidOperationException($"当前 HTTP 客户端尚不支持协议版本 '{version}'。");
            }
        }

        return normalizedVersions;
    }

    private static string NormalizePreferredProtocolVersion(string? preferredProtocolVersion)
    {
        return string.IsNullOrWhiteSpace(preferredProtocolVersion)
            ? ProtocolVersion.Current
            : preferredProtocolVersion;
    }

    private static void ValidateProtocolVersionConfiguration(string preferredProtocolVersion, IReadOnlyList<string> supportedProtocolVersions)
    {
        if (!ProtocolVersion.IsSupportedStreamableHttpVersion(preferredProtocolVersion))
        {
            throw new InvalidOperationException($"当前 HTTP 客户端尚不支持首选协议版本 '{preferredProtocolVersion}'。");
        }

        if (!supportedProtocolVersions.Contains(preferredProtocolVersion, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"首选协议版本 '{preferredProtocolVersion}' 必须包含在支持版本集合中。");
        }
    }
}
