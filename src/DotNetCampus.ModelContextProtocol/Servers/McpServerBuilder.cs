using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Transports;
using DotNetCampus.ModelContextProtocol.Transports.Http;
using DotNetCampus.ModelContextProtocol.Transports.Stdio;
using DotNetCampus.ModelContextProtocol.Transports.InProcess;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 用于构建 MCP 服务器的生成器。
/// </summary>
public class McpServerBuilder(string serverName, string serverVersion)
{
    private McpServerContext? _context;
    private readonly List<IMcpServerTransport> _transports = [];
    private HttpServerTransportOptions? _httpOptions;
    private readonly McpServerToolsProvider _tools = new();
    private readonly McpServerResourcesProvider _resources = new();
    private McpRequestHandlersBuilder? _requestHandlers;

    /// <summary>
    /// 允许此 MCP 服务器通过标准输入/输出流提供服务。
    /// </summary>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    public McpServerBuilder WithStdio()
    {
        var context = _context ?? new McpServerContext();
        var transport = new StdioServerTransport(new StdioServerTransportOptions(), context.Logger);
        _transports.Add(transport);
        return this;
    }

    /// <summary>
    /// 允许此 MCP 服务器通过 HTTP 提供服务。
    /// </summary>
    /// <param name="port">MCP 服务器将监听 http://localhost:{port} 上的请求。</param>
    /// <param name="endpoint">
    /// MCP 服务器将监听的路由端点，例如指定为 mcp 时，完整的 URL 为 http://localhost:{port}/mcp。<br/>
    /// 所有的 MCP 请求都将发送到该端点；除非客户端使用旧版本（2024-11-05）的 SSE 协议传输时，会自动改为使用 /mcp/sse 端点。<br/>
    /// </param>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    public McpServerBuilder WithHttp(int port, [StringSyntax("Route")] string endpoint)
    {
        _httpOptions = new HttpServerTransportOptions
        {
            UrlPrefixes = [$"http://localhost:{port}/", $"http://127.0.0.1:{port}/", $"http://[::1]:{port}/"],
            Endpoint = endpoint,
        };
        return this;
    }

    /// <summary>
    /// 配置自定义的 JSON 序列化上下文。
    /// </summary>
    /// <param name="jsonSerializerContext">JSON 序列化上下文</param>
    public McpServerBuilder WithJsonSerializer(JsonSerializerContext jsonSerializerContext)
    {
        var jsonSerializer = new McpServerToolJsonSerializer(jsonSerializerContext);
        _context = _context switch
        {
            null => new McpServerContext
            {
                JsonSerializer = jsonSerializer,
                JsonSerializerTypeName = jsonSerializerContext.GetType().FullName,
            },
            var c => c with
            {
                JsonSerializer = jsonSerializer,
                JsonSerializerTypeName = jsonSerializerContext.GetType().FullName,
            },
        };
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
    public McpServerBuilder WithResources(Action<McpServerResourcesBuilder> resourceBuilder)
    {
        var builder = new McpServerResourcesBuilder(_context, _resources);
        resourceBuilder(builder);
        _context = builder.Context;
        return this;
    }

    /// <summary>
    /// 配置 MCP 服务器的工具和相关选项。
    /// </summary>
    /// <param name="toolsBuilder">用于配置工具的生成器。</param>
    /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
    public McpServerBuilder WithTools(Action<McpServerToolsBuilder> toolsBuilder)
    {
        var builder = new McpServerToolsBuilder(_context, _tools);
        toolsBuilder(builder);
        _context = builder.Context;
        return this;
    }

    /// <summary>
    /// 构建 MCP 服务器实例。
    /// </summary>
    /// <returns>构建的 MCP 服务器实例。</returns>
    public McpServer Build()
    {
        var context = _context ?? new McpServerContext();
        
        // 如果配置了 HTTP，添加到传输层列表
        if (_httpOptions is not null)
        {
            _transports.Add(new HttpServerTransport(context, _httpOptions));
        }
        
        var server = new McpServer
        {
            ServerName = serverName,
            ServerVersion = serverVersion,
            Context = context,
            Transports = _transports,
            Tools = _tools,
            Resources = _resources,
        };
        context.Handlers = _requestHandlers is { } requestHandlers
            ? requestHandlers(server, new McpRequestHandlers(server))
            : new McpRequestHandlers(server);
        return server;
    }
}

/// <summary>
/// MCP 服务器资源构建器。
/// </summary>
public class McpServerResourcesBuilder
{
    private readonly McpServerResourcesProvider _resources;

    internal McpServerResourcesBuilder(McpServerContext? originalContext, McpServerResourcesProvider resources)
    {
        Context = originalContext;
        _resources = resources;
    }

    internal McpServerContext? Context { get; private set; }

    /// <summary>
    /// 获取资源提供程序。
    /// </summary>
    public IMcpServerResourcesProvider Resources => _resources;

    /// <summary>
    /// 配置服务提供程序。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    public McpServerResourcesBuilder WithServiceProvider(IServiceProvider serviceProvider)
    {
        Context = Context switch
        {
            null => new McpServerContext
            {
                ServiceProvider = serviceProvider,
            },
            var c => c with
            {
                ServiceProvider = serviceProvider,
            },
        };
        return this;
    }

    /// <summary>
    /// 添加资源（由源生成器拦截）
    /// </summary>
    public McpServerResourcesBuilder WithResource<TMcpServerResourceType>(Func<TMcpServerResourceType> resourceFactory,
        CreationMode creationMode = CreationMode.Singleton)
        where TMcpServerResourceType : class
    {
        throw new InvalidOperationException(
            "拦截器未能成功拦截 WithResource<T> 方法调用。请确保：1. 所有需要被拦截的方法均已标记了 [McpServerResourceAttribute] 特性。2. 编译项目时没有出现与 DotNetCampus.ModelContextProtocol 源生成器相关的警告或错误（CS8785;CS9057）。");
    }

    /// <summary>
    /// 添加资源（由源生成器调用）
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public McpServerResourcesBuilder WithResource<TMcpServerResourceType>(IMcpServerResource resource)
        where TMcpServerResourceType : class
    {
        var name = resource.ResourceName;
        if (!_resources.TryAdd(name, resource))
        {
            throw new InvalidOperationException($"已存在名称为 \"{name}\" 的 MCP 服务器资源，无法重复添加同名资源。");
        }
        return this;
    }
}

/// <summary>
/// MCP 服务器工具构建器
/// </summary>
public class McpServerToolsBuilder
{
    private readonly McpServerToolsProvider _tools;

    internal McpServerToolsBuilder(McpServerContext? originalContext, McpServerToolsProvider tools)
    {
        Context = originalContext;
        _tools = tools;
    }

    internal McpServerContext? Context { get; private set; }

    /// <summary>
    /// 获取工具提供程序。
    /// </summary>
    public IMcpServerToolsProvider Tools => _tools;

    /// <summary>
    /// 配置服务提供程序。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    public McpServerToolsBuilder WithServiceProvider(IServiceProvider serviceProvider)
    {
        Context = Context switch
        {
            null => new McpServerContext
            {
                ServiceProvider = serviceProvider,
            },
            var c => c with
            {
                ServiceProvider = serviceProvider,
            },
        };
        return this;
    }

    /// <summary>
    /// 添加工具（由源生成器拦截）。
    /// </summary>
    public McpServerToolsBuilder WithTool<TMcpServerToolType>(Func<TMcpServerToolType> toolFactory,
        CreationMode creationMode = CreationMode.Singleton)
        where TMcpServerToolType : class
    {
        throw new InvalidOperationException(
            "拦截器未能成功拦截 WithTool<T> 方法调用。请确保：1. 所有需要被拦截的方法均已标记了 [McpServerToolAttribute] 特性。2. 编译项目时没有出现与 DotNetCampus.ModelContextProtocol 源生成器相关的警告或错误（CS8785;CS9057）。");
    }

    /// <summary>
    /// 添加工具（由源生成器调用）。
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public McpServerToolsBuilder WithTool<TMcpServerToolType>(IMcpServerTool tool)
        where TMcpServerToolType : class
    {
        var name = tool.ToolName;
        if (!_tools.TryAdd(name, tool))
        {
            throw new InvalidOperationException($"已存在名称为 \"{name}\" 的 MCP 服务器工具，无法重复添加同名工具。");
        }
        return this;
    }
}

/// <summary>
/// 用于构建 MCP 请求处理程序的委托。
/// </summary>
/// <param name="mcpServer">MCP 服务器实例。</param>
/// <param name="defaultHandlers">默认的 MCP 请求处理程序。</param>
/// <returns>构建的 MCP 请求处理程序。</returns>
public delegate McpRequestHandlers McpRequestHandlersBuilder(McpServer mcpServer, McpRequestHandlers defaultHandlers);
