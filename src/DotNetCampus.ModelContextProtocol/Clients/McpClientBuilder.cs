using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Transports;
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
    private IServiceProvider? _serviceProvider;
    private Func<IClientTransportManager, IClientTransport>? _transportFactory;
    private ClientCapabilities _capabilities = new();

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
    /// 使用自定义的传输层。
    /// </summary>
    /// <param name="transportFactory">传输层工厂方法。</param>
    /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
    public McpClientBuilder WithTransport(Func<IClientTransportManager, IClientTransport> transportFactory)
    {
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

        var transportManager = new ClientTransportManager(context);
        context.Transport = transportManager;

        var transport = _transportFactory(transportManager);
        transportManager.SetTransport(transport);

        return new McpClient(context)
        {
            ClientName = _clientName,
            ClientVersion = _clientVersion,
            Capabilities = _capabilities,
        };
    }
}
