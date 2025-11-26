using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Transports;
using DotNetCampus.ModelContextProtocol.Transports.Http;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 用于构建 MCP 服务器的生成器。
/// </summary>
public class McpServerBuilder(string serverName, string serverVersion)
{
    private readonly List<Func<ServerTransportManager, IServerTransport>> _transportFactories = [];
    private readonly McpServerToolsProvider _tools = new();
    private readonly McpServerResourcesProvider _resources = new();
    private IMcpLogger? _logger;
    private IMcpServerToolJsonSerializer? _jsonSerializer;
    private string? _jsonSerializerTypeName;
    private IServiceProvider? _serviceProvider;
    private McpRequestHandlersBuilder? _requestHandlers;

    /// <summary>
    /// 允许此 MCP 服务器通过标准输入/输出流提供服务。
    /// </summary>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    public McpServerBuilder WithStdio()
    {
        return this;
    }

    /// <summary>
    /// 允许此 MCP 服务器通过 HTTP 提供服务。
    /// </summary>
    /// <param name="port">MCP 服务器将监听 http://localhost:{port} 上的请求。</param>
    /// <param name="endPoint">
    /// MCP 服务器将监听的路由端点，例如指定为 mcp 时，完整的 URL 为 http://localhost:{port}/mcp。<br/>
    /// 所有的 MCP 请求都将发送到该端点；除非客户端使用旧版本（2024-11-05）的 SSE 协议传输时，会自动改为使用 /mcp/sse 端点。<br/>
    /// </param>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    /// <remarks>
    /// 在无依赖的情况下，本 MCP 库只支持监听本机回环地址（localhost）的请求。<br/>
    /// 如果需要监听其他地址，请安装 DotNetCampus.ModelContextProtocol.AspNetCore / DotNetCampus.ModelContextProtocol.TouchSocket 等包。
    /// </remarks>
    public McpServerBuilder WithLocalHostHttp(int port, [StringSyntax("Route")] string endPoint)
    {
        _transportFactories.Add(m => new LocalHostStreamableHttpTransport(m, new LocalHostHttpTransportOptions
        {
            Port = port,
            EndPoint = endPoint,
        }));
        return this;
    }

    /// <summary>
    /// 配置 MCP 服务器的日志记录器。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    public McpServerBuilder WithLogger(IMcpLogger logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// 配置自定义的 JSON 序列化上下文。
    /// </summary>
    /// <param name="jsonSerializerContext">JSON 序列化上下文。</param>
    public McpServerBuilder WithJsonSerializer(JsonSerializerContext jsonSerializerContext)
    {
        var jsonSerializer = new McpServerToolJsonSerializer(jsonSerializerContext);
        _jsonSerializer = jsonSerializer;
        _jsonSerializerTypeName = jsonSerializerContext.GetType().FullName;
        return this;
    }

    /// <summary>
    /// 配置 MCP 服务器的服务提供器。
    /// </summary>
    /// <param name="services">服务提供器。</param>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    public McpServerBuilder WithServices(IServiceProvider services)
    {
        _serviceProvider = services;
        return this;
    }

    /// <summary>
    /// 配置自定义的 MCP 请求处理程序。
    /// </summary>
    /// <param name="handlers">创建自定义的 MCP 请求处理程序的工厂。</param>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    public McpServerBuilder WithRequestHandlers(McpRequestHandlersBuilder handlers)
    {
        _requestHandlers = handlers;
        return this;
    }

    /// <summary>
    /// 配置 MCP 服务器的资源和相关选项。
    /// </summary>
    /// <param name="resourceBuilder">用于配置资源的生成器。</param>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    public McpServerBuilder WithResources(Action<IMcpServerResourcesBuilder> resourceBuilder)
    {
        var builder = new McpServerResourcesBuilder(this);
        resourceBuilder(builder);
        return this;
    }

    /// <summary>
    /// 配置 MCP 服务器的工具和相关选项。
    /// </summary>
    /// <param name="toolsBuilder">用于配置工具的生成器。</param>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    public McpServerBuilder WithTools(Action<IMcpServerToolsBuilder> toolsBuilder)
    {
        var builder = new McpServerToolsBuilder(this);
        toolsBuilder(builder);
        return this;
    }

    /// <summary>
    /// 构建 MCP 服务器实例。
    /// </summary>
    /// <returns>构建的 MCP 服务器实例。</returns>
    public McpServer Build()
    {
        // Context
        var context = new McpServerContext
        {
            Logger = _logger ?? EmptyLogger.Instance,
            JsonSerializer = _jsonSerializer ?? new McpServerToolJsonSerializer(),
            JsonSerializerTypeName = _jsonSerializerTypeName,
            ServiceProvider = _serviceProvider,
        };

        // Server
        var server = new McpServer(context)
        {
            ServerName = serverName,
            ServerVersion = serverVersion,
            Tools = _tools,
            Resources = _resources,
        };

        // Context (Handlers + Transport)
        context.Handlers = _requestHandlers is { } requestHandlers
            ? requestHandlers(server, new McpRequestHandlers(server))
            : new McpRequestHandlers(server);
        var transportManager = new ServerTransportManager(context);
        context.Transport = transportManager;
        foreach (var factory in _transportFactories)
        {
            var transport = factory(transportManager);
            transportManager.Add(transport);
        }

        return server;
    }

    private class McpServerResourcesBuilder : IMcpServerResourcesBuilder
    {
        private readonly McpServerBuilder _builder;

        internal McpServerResourcesBuilder(McpServerBuilder builder)
        {
            _builder = builder;
        }

        public IMcpServerResourcesProvider Resources => _builder._resources;

        public IMcpServerResourcesBuilder WithResource<TMcpServerResourceType>(Func<TMcpServerResourceType> resourceFactory,
            CreationMode creationMode = CreationMode.Singleton)
            where TMcpServerResourceType : class
        {
            throw new InvalidOperationException(
                "拦截器未能成功拦截 WithResource<T> 方法调用。请确保：1. 所有需要被拦截的方法均已标记了 [McpServerResourceAttribute] 特性。2. 编译项目时没有出现与 DotNetCampus.ModelContextProtocol 源生成器相关的警告或错误（CS8785;CS9057）。");
        }

        public IMcpServerResourcesBuilder WithResource<TMcpServerResourceType>(IMcpServerResource resource)
            where TMcpServerResourceType : class
        {
            var name = resource.ResourceName;
            if (!_builder._resources.TryAdd(name, resource))
            {
                throw new InvalidOperationException($"已存在名称为 \"{name}\" 的 MCP 服务器资源，无法重复添加同名资源。");
            }
            return this;
        }
    }

    private class McpServerToolsBuilder : IMcpServerToolsBuilder
    {
        private readonly McpServerBuilder _builder;

        internal McpServerToolsBuilder(McpServerBuilder builder)
        {
            _builder = builder;
        }

        public IMcpServerToolsProvider Tools => _builder._tools;

        public IMcpServerToolsBuilder WithTool<TMcpServerToolType>(Func<TMcpServerToolType> toolFactory,
            CreationMode creationMode = CreationMode.Singleton)
            where TMcpServerToolType : class
        {
            throw new InvalidOperationException(
                "拦截器未能成功拦截 WithTool<T> 方法调用。请确保：1. 所有需要被拦截的方法均已标记了 [McpServerToolAttribute] 特性。2. 编译项目时没有出现与 DotNetCampus.ModelContextProtocol 源生成器相关的警告或错误（CS8785;CS9057）。");
        }

        public IMcpServerToolsBuilder WithTool<TMcpServerToolType>(IMcpServerTool tool)
            where TMcpServerToolType : class
        {
            var name = tool.ToolName;
            if (!_builder._tools.TryAdd(name, tool))
            {
                throw new InvalidOperationException($"已存在名称为 \"{name}\" 的 MCP 服务器工具，无法重复添加同名工具。");
            }
            return this;
        }
    }
}

/// <summary>
/// MCP 服务器资源构建器。
/// </summary>
public interface IMcpServerResourcesBuilder
{
    /// <summary>
    /// 获取 MCP 服务器资源提供程序。可用于添加或获取 MCP 资源。
    /// </summary>
    IMcpServerResourcesProvider Resources { get; }

    /// <summary>
    /// 添加资源（由源生成器拦截）
    /// </summary>
    IMcpServerResourcesBuilder WithResource<TMcpServerResourceType>(Func<TMcpServerResourceType> resourceFactory,
        CreationMode creationMode = CreationMode.Singleton)
        where TMcpServerResourceType : class;

    /// <summary>
    /// 添加资源（由源生成器调用）
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    IMcpServerResourcesBuilder WithResource<TMcpServerResourceType>(IMcpServerResource resource)
        where TMcpServerResourceType : class;
}

/// <summary>
/// MCP 服务器工具构建器
/// </summary>
public interface IMcpServerToolsBuilder
{
    /// <summary>
    /// 获取 MCP 服务器工具提供程序。可用于添加或获取 MCP 工具。
    /// </summary>
    IMcpServerToolsProvider Tools { get; }

    /// <summary>
    /// 添加工具（由源生成器拦截）。
    /// </summary>
    IMcpServerToolsBuilder WithTool<TMcpServerToolType>(Func<TMcpServerToolType> toolFactory,
        CreationMode creationMode = CreationMode.Singleton)
        where TMcpServerToolType : class;

    /// <summary>
    /// 添加工具（由源生成器调用）。
    /// </summary>
    IMcpServerToolsBuilder WithTool<TMcpServerToolType>(IMcpServerTool tool)
        where TMcpServerToolType : class;
}

/// <summary>
/// 用于构建 MCP 请求处理程序的委托。
/// </summary>
/// <param name="mcpServer">MCP 服务器实例。</param>
/// <param name="defaultHandlers">默认的 MCP 请求处理程序。</param>
/// <returns>构建的 MCP 请求处理程序。</returns>
public delegate McpRequestHandlers McpRequestHandlersBuilder(McpServer mcpServer, McpRequestHandlers defaultHandlers);

file sealed class EmptyLogger : IMcpLogger
{
    public static readonly EmptyLogger Instance = new();

    private EmptyLogger()
    {
    }

    public bool IsEnabled(LoggingLevel loggingLevel) => false;

    public void Log<TState>(LoggingLevel loggingLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }
}
