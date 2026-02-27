# MCP 传输层架构设计

> 本文档描述 DotNetCampus.ModelContextProtocol 库的传输层抽象设计，支持 stdio、HTTP (Streamable HTTP + SSE)、InProcess 和 IPC 等多种传输协议。

## 📋 目录

- [设计目标](#设计目标)
- [传输层协议对比](#传输层协议对比)
- [核心设计理念](#核心设计理念)
- [接口设计](#接口设计)
- [实现示例](#实现示例)
- [使用方式](#使用方式)
- [扩展指南](#扩展指南)
- [迁移计划](#迁移计划)

## 设计目标

### 🎯 三个核心目标

1. **易于选择**：业务层可以像挑选购物车商品一样简单地选择传输层协议
2. **易于扩展**：扩展新的传输层协议最好能简单到一个文件就能完成
3. **易于维护**：本库的维护者可以轻而易举地维护现有的所有传输层协议

### ✨ 设计原则

- **职责分离**：传输层只负责消息的传输，不处理业务逻辑
- **统一抽象**：所有传输层实现统一的接口契约
- **零耦合**：业务层不应依赖具体的传输层实现
- **最小依赖**：每个传输层实现应该是独立的，避免相互依赖

## 传输层协议对比

### 协议特性矩阵

| 特性             | stdio                | HTTP (Streamable)    | HTTP (SSE - Legacy)  | InProcess        | IPC (Named Pipe) |
| ---------------- | -------------------- | -------------------- | -------------------- | ---------------- | ---------------- |
| **连接模式**     | 一对一（专用）       | 多对一（共享）       | 多对一（共享）       | 一对一（专用）   | 一对一（专用）   |
| **会话管理**     | 隐式（进程生命周期） | 显式（Session ID）   | 显式（Session ID）   | 隐式（对象引用） | 显式（连接 ID）  |
| **双向通信**     | ✅ 全双工             | ✅ 全双工（SSE+POST） | ✅ 全双工（SSE+POST） | ✅ 全双工         | ✅ 全双工         |
| **服务器推送**   | ✅ stdout             | ✅ SSE                | ✅ SSE                | ✅ 直接调用       | ✅ 命名管道写入   |
| **并发连接**     | ❌ 单连接             | ✅ 多连接             | ✅ 多连接             | ❌ 单连接         | ⚠️ 可多连接       |
| **跨进程**       | ✅ 是                 | ✅ 是                 | ✅ 是                 | ❌ 否             | ✅ 是             |
| **跨网络**       | ❌ 否                 | ✅ 是                 | ✅ 是                 | ❌ 否             | ⚠️ 可跨机器       |
| **消息边界**     | ✅ 换行分隔           | ✅ HTTP 请求          | ✅ HTTP 请求          | ✅ 对象边界       | ⚠️ 需手动处理     |
| **安全性**       | ⚠️ 进程隔离           | ✅ HTTPS + 认证       | ✅ HTTPS + 认证       | ⚠️ 内存共享       | ⚠️ 本地权限       |
| **可靠性**       | ✅ 管道可靠           | ⚠️ 网络不可靠         | ⚠️ 网络不可靠         | ✅ 内存可靠       | ✅ 管道可靠       |
| **性能**         | ⭐⭐⭐⭐⭐                | ⭐⭐⭐                  | ⭐⭐⭐                  | ⭐⭐⭐⭐⭐⭐           | ⭐⭐⭐⭐             |
| **MCP 协议版本** | 所有版本             | 2025-03-26+          | 2024-11-05           | 所有版本         | 所有版本         |

### 连接模式说明

#### 一对一（专用连接）

- **stdio**：客户端启动服务器进程，拥有独占的 stdin/stdout 通道
- **InProcess**：客户端和服务器在同一进程内，通过对象引用直接通信
- **IPC (Named Pipe)**：每个客户端连接到独立的命名管道实例

**特点**：
- ✅ 无需会话管理（连接即会话）
- ✅ 实现简单，性能高
- ❌ 无法共享服务器资源

#### 多对一（共享服务器）

- **HTTP (Streamable/SSE)**：多个客户端连接到同一个 HTTP 服务器
- **IPC（中继模式）**：IPC 作为隧道透传 HTTP 协议时，可能需要支持多对一

**特点**：
- ✅ 资源共享，可横向扩展
- ✅ 适合 Web 场景和多用户场景
- ⚠️ 需要显式的会话管理（Session ID）
- ⚠️ 需要处理并发和线程安全

## 核心设计理念

### 🏗️ 分层架构

```
┌─────────────────────────────────────────────────────────────┐
│                      业务层 (McpServer)                       │
│   - 处理 MCP 协议逻辑（initialize, tools, resources 等）      │
│   - 不关心底层传输方式                                         │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │ IMcpServerTransport
                              │
┌─────────────────────────────────────────────────────────────┐
│                     传输层抽象 (接口)                         │
│   - IMcpServerTransport: 服务器传输层接口                     │
│   - IMcpClientTransport: 客户端传输层接口                     │
│   - TransportMessage: 统一的消息载体                          │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │ 实现
                              │
┌─────────────┬──────────────┬──────────────┬──────────────┐
│  Stdio      │  Http        │  InProcess   │  IPC         │
│  Transport  │  Transport   │  Transport   │  Transport   │
├─────────────┼──────────────┼──────────────┼──────────────┤
│ 一对一专用   │ 多对一共享    │ 一对一专用    │ 一对一专用    │
│ 进程启动     │ HTTP 服务器   │ 对象引用      │ 命名管道      │
└─────────────┴──────────────┴──────────────┴──────────────┘
```

### 🔑 关键概念

#### 1. 消息通道（Message Channel）

所有传输层都提供**消息级别**的抽象，而非字节流抽象：

- **发送消息**：`SendMessageAsync(message, cancellationToken)`
- **接收消息**：`ChannelReader<TransportMessage> MessageReader`

#### 2. 会话管理（Session Management）

传输层分为两类：

- **隐式会话**（一对一）：连接即会话，无需显式管理
  - stdio（进程生命周期）
  - InProcess（对象引用）
  - IPC 单连接模式（管道生命周期）
  
- **显式会话**（多对一）：需要 Session ID 来区分客户端
  - HTTP (Streamable/SSE)
  - IPC 多连接模式（可选）

#### 3. 连接上下文（Connection Context）

每个传输层可以提供额外的上下文信息（通过依赖注入）：

```csharp
// HTTP 上下文
public record HttpServerTransportContext
{
    public required string? SessionId { get; init; }
    public required NameValueCollection Headers { get; init; }
}

// stdio 上下文
public record StdioServerTransportContext
{
    public int ProcessId { get; init; }
    public string? WorkingDirectory { get; init; }
}

// IPC 上下文
public record IpcServerTransportContext
{
    public string PipeName { get; init; }
    public string? ClientIdentity { get; init; }
}
```

## 接口设计

### 核心接口

```csharp
/// <summary>
/// 传输消息的统一载体<br/>
/// Unified transport message carrier
/// </summary>
public sealed class TransportMessage
{
    /// <summary>
    /// JSON-RPC 消息内容<br/>
    /// JSON-RPC message content
    /// </summary>
    public required JsonRpcMessage Message { get; init; }

    /// <summary>
    /// 会话 ID（仅用于多对一的传输层）<br/>
    /// Session ID (only for many-to-one transports)
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// 传输层特定的元数据（可选）<br/>
    /// Transport-specific metadata (optional)
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// MCP 服务器传输层接口<br/>
/// MCP server transport interface
/// </summary>
public interface IMcpServerTransport : IAsyncDisposable
{
    /// <summary>
    /// 传输层名称（用于日志和诊断）<br/>
    /// Transport name (for logging and diagnostics)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 是否支持多对一连接<br/>
    /// Whether this transport supports many-to-one connections
    /// </summary>
    bool SupportsManyToOne { get; }

    /// <summary>
    /// 是否支持服务器推送<br/>
    /// Whether this transport supports server push
    /// </summary>
    bool SupportsServerPush { get; }

    /// <summary>
    /// 消息读取器（接收来自客户端的消息）<br/>
    /// Message reader (receive messages from clients)
    /// </summary>
    ChannelReader<TransportMessage> MessageReader { get; }

    /// <summary>
    /// 发送消息到客户端<br/>
    /// Send a message to the client
    /// </summary>
    /// <param name="message">要发送的消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task SendMessageAsync(TransportMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动传输层（开始监听）<br/>
    /// Start the transport (begin listening)
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止传输层<br/>
    /// Stop the transport
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// MCP 客户端传输层接口<br/>
/// MCP client transport interface
/// </summary>
public interface IMcpClientTransport : IAsyncDisposable
{
    /// <summary>
    /// 传输层名称（用于日志和诊断）<br/>
    /// Transport name (for logging and diagnostics)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 会话 ID（如果传输层支持会话管理）<br/>
    /// Session ID (if transport supports session management)
    /// </summary>
    string? SessionId { get; }

    /// <summary>
    /// 消息读取器（接收来自服务器的消息）<br/>
    /// Message reader (receive messages from server)
    /// </summary>
    ChannelReader<TransportMessage> MessageReader { get; }

    /// <summary>
    /// 发送消息到服务器<br/>
    /// Send a message to the server
    /// </summary>
    /// <param name="message">要发送的消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task SendMessageAsync(TransportMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 连接到服务器<br/>
    /// Connect to the server
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开连接<br/>
    /// Disconnect from the server
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
```

### 传输层配置接口

```csharp
/// <summary>
/// 传输层配置基类<br/>
/// Base class for transport configurations
/// </summary>
public abstract record TransportOptions
{
    /// <summary>
    /// 传输层名称（可选，用于日志）<br/>
    /// Transport name (optional, for logging)
    /// </summary>
    public string? Name { get; init; }
}

/// <summary>
/// stdio 传输层配置<br/>
/// stdio transport configuration
/// </summary>
public sealed record StdioTransportOptions : TransportOptions
{
    /// <summary>
    /// 是否捕获 stderr（用于日志）<br/>
    /// Whether to capture stderr (for logging)
    /// </summary>
    public bool CaptureStderr { get; init; } = true;

    /// <summary>
    /// 工作目录（服务器端）<br/>
    /// Working directory (server side)
    /// </summary>
    public string? WorkingDirectory { get; init; }
}

/// <summary>
/// HTTP 传输层配置<br/>
/// HTTP transport configuration
/// </summary>
public sealed record HttpTransportOptions : TransportOptions
{
    /// <summary>
    /// 基础 URL<br/>
    /// Base URL
    /// </summary>
    public required string BaseUrl { get; init; }

    /// <summary>
    /// MCP 端点路径<br/>
    /// MCP endpoint path
    /// </summary>
    public string Endpoint { get; init; } = "mcp";

    /// <summary>
    /// 是否启用 CORS<br/>
    /// Whether to enable CORS
    /// </summary>
    public bool EnableCors { get; init; } = true;

    /// <summary>
    /// 是否支持旧协议（2024-11-05 SSE）<br/>
    /// Whether to support legacy protocol (2024-11-05 SSE)
    /// </summary>
    public bool SupportLegacyProtocol { get; init; } = true;
}

/// <summary>
/// InProcess 传输层配置<br/>
/// InProcess transport configuration
/// </summary>
public sealed record InProcessTransportOptions : TransportOptions
{
    /// <summary>
    /// 消息缓冲区大小<br/>
    /// Message buffer size
    /// </summary>
    public int BufferSize { get; init; } = 100;
}

/// <summary>
/// IPC (Named Pipe) 传输层配置<br/>
/// IPC (Named Pipe) transport configuration
/// </summary>
public sealed record IpcTransportOptions : TransportOptions
{
    /// <summary>
    /// 命名管道名称<br/>
    /// Named pipe name
    /// </summary>
    public required string PipeName { get; init; }

    /// <summary>
    /// 是否支持多对一连接<br/>
    /// Whether to support many-to-one connections
    /// </summary>
    public bool AllowMultipleConnections { get; init; } = false;

    /// <summary>
    /// 最大连接数（仅当 AllowMultipleConnections = true 时有效）<br/>
    /// Maximum number of connections (only when AllowMultipleConnections = true)
    /// </summary>
    public int MaxConnections { get; init; } = 10;
}
```

## 实现示例

### 示例 1: stdio 传输层（一对一）

```csharp
/// <summary>
/// stdio 传输层实现（服务器端）<br/>
/// stdio transport implementation (server side)
/// </summary>
public sealed class StdioServerTransport : IMcpServerTransport
{
    private readonly StdioTransportOptions _options;
    private readonly ILogger _logger;
    private readonly Channel<TransportMessage> _messageChannel;
    private readonly CancellationTokenSource _cts = new();

    public string Name => _options.Name ?? "stdio";
    public bool SupportsManyToOne => false; // stdio 是一对一的
    public bool SupportsServerPush => true;
    public ChannelReader<TransportMessage> MessageReader => _messageChannel.Reader;

    public StdioServerTransport(StdioTransportOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
        _messageChannel = Channel.CreateUnbounded<TransportMessage>();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.Info($"[{Name}] Starting stdio transport");

        // 在后台线程读取 stdin
        _ = Task.Run(async () =>
        {
            using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

            try
            {
                while (!linkedCts.Token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(linkedCts.Token);
                    if (line is null) break; // EOF

                    // 解析 JSON-RPC 消息
                    var message = JsonSerializer.Deserialize(line, McpServerRequestJsonContext.Default.JsonRpcRequest);
                    if (message is not null)
                    {
                        await _messageChannel.Writer.WriteAsync(new TransportMessage
                        {
                            Message = message,
                            SessionId = null, // stdio 不需要 Session ID
                        }, linkedCts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                _logger.Error($"[{Name}] Error reading from stdin", ex);
            }
            finally
            {
                _messageChannel.Writer.Complete();
            }
        }, cancellationToken);

        // 如果需要，捕获 stderr
        if (_options.CaptureStderr)
        {
            _ = Task.Run(async () =>
            {
                using var errorReader = new StreamReader(Console.OpenStandardError(), Encoding.UTF8);
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

                try
                {
                    while (!linkedCts.Token.IsCancellationRequested)
                    {
                        var line = await errorReader.ReadLineAsync(linkedCts.Token);
                        if (line is null) break;
                        _logger.Debug($"[{Name}][stderr] {line}");
                    }
                }
                catch (OperationCanceledException) { }
            }, cancellationToken);
        }
    }

    public async Task SendMessageAsync(TransportMessage message, CancellationToken cancellationToken = default)
    {
        // 将消息序列化并写入 stdout
        var json = JsonSerializer.Serialize(message.Message, McpServerResponseJsonContext.Default.JsonRpcResponse);
        
        // 确保原子性：先写入内存，再一次性输出
        await Console.Out.WriteLineAsync(json.AsMemory(), cancellationToken);
        await Console.Out.FlushAsync(cancellationToken);

        _logger.Debug($"[{Name}] Sent message to stdout");
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts.Cancel();
        _messageChannel.Writer.Complete();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
    }
}
```

### 示例 2: InProcess 传输层（一对一）

```csharp
/// <summary>
/// InProcess 传输层实现（客户端和服务器在同一进程）<br/>
/// InProcess transport implementation (client and server in the same process)
/// </summary>
public sealed class InProcessTransport : IMcpServerTransport, IMcpClientTransport
{
    private readonly InProcessTransportOptions _options;
    private readonly Channel<TransportMessage> _serverToClient;
    private readonly Channel<TransportMessage> _clientToServer;

    public string Name => _options.Name ?? "in-process";
    public bool SupportsManyToOne => false;
    public bool SupportsServerPush => true;
    public string? SessionId => null; // InProcess 不需要 Session ID

    // 服务器端读取客户端消息
    ChannelReader<TransportMessage> IMcpServerTransport.MessageReader => _clientToServer.Reader;

    // 客户端读取服务器消息
    ChannelReader<TransportMessage> IMcpClientTransport.MessageReader => _serverToClient.Reader;

    public InProcessTransport(InProcessTransportOptions options)
    {
        _options = options;
        var bufferSize = options.BufferSize;
        _serverToClient = Channel.CreateBounded<TransportMessage>(bufferSize);
        _clientToServer = Channel.CreateBounded<TransportMessage>(bufferSize);
    }

    // 服务器端发送消息到客户端
    async Task IMcpServerTransport.SendMessageAsync(TransportMessage message, CancellationToken cancellationToken)
    {
        await _serverToClient.Writer.WriteAsync(message, cancellationToken);
    }

    // 客户端发送消息到服务器
    async Task IMcpClientTransport.SendMessageAsync(TransportMessage message, CancellationToken cancellationToken)
    {
        await _clientToServer.Writer.WriteAsync(message, cancellationToken);
    }

    Task IMcpServerTransport.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task IMcpServerTransport.StopAsync(CancellationToken cancellationToken)
    {
        _serverToClient.Writer.Complete();
        _clientToServer.Writer.Complete();
        return Task.CompletedTask;
    }

    Task IMcpClientTransport.ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task IMcpClientTransport.DisconnectAsync(CancellationToken cancellationToken)
    {
        _serverToClient.Writer.Complete();
        _clientToServer.Writer.Complete();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _serverToClient.Writer.Complete();
        _clientToServer.Writer.Complete();
        await Task.CompletedTask;
    }
}
```

### 示例 3: HTTP 传输层（多对一）- 重构版

```csharp
/// <summary>
/// HTTP 传输层实现（支持多对一连接）<br/>
/// HTTP transport implementation (supports many-to-one connections)
/// </summary>
public sealed class HttpServerTransport : IMcpServerTransport
{
    private readonly HttpTransportOptions _options;
    private readonly ILogger _logger;
    private readonly HttpListener _listener = new();
    private readonly Channel<TransportMessage> _messageChannel;
    private readonly ConcurrentDictionary<string, SseSession> _sessions = new();
    private readonly CancellationTokenSource _cts = new();

    public string Name => _options.Name ?? "http";
    public bool SupportsManyToOne => true;
    public bool SupportsServerPush => true;
    public ChannelReader<TransportMessage> MessageReader => _messageChannel.Reader;

    public HttpServerTransport(HttpTransportOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
        _messageChannel = Channel.CreateUnbounded<TransportMessage>();
        _listener.Prefixes.Add(options.BaseUrl);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listener.Start();
        _logger.Info($"[{Name}] HTTP server listening on {_options.BaseUrl}");

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        while (!linkedCts.Token.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context, linkedCts.Token), linkedCts.Token);
            }
            catch (HttpListenerException) when (linkedCts.Token.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var endpoint = context.Request.Url?.AbsolutePath;
        var method = context.Request.HttpMethod;

        try
        {
            // 新协议：Streamable HTTP (2025-03-26+)
            if (endpoint?.Equals($"/{_options.Endpoint}", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (method == "GET")
                    await HandleSseConnectionAsync(context, cancellationToken);
                else if (method == "POST")
                    await HandlePostRequestAsync(context, cancellationToken);
                else if (method == "DELETE")
                    await HandleDeleteSessionAsync(context, cancellationToken);
                else
                    RespondWithError(context, HttpStatusCode.MethodNotAllowed);
            }
            // 旧协议：HTTP+SSE (2024-11-05) - 可选支持
            else if (_options.SupportLegacyProtocol)
            {
                if (method == "GET" && endpoint?.EndsWith("/sse", StringComparison.OrdinalIgnoreCase) == true)
                    await HandleLegacySseAsync(context, cancellationToken);
                else if (method == "POST" && endpoint?.EndsWith("/messages", StringComparison.OrdinalIgnoreCase) == true)
                    await HandleLegacyPostAsync(context, cancellationToken);
                else
                    RespondWithError(context, HttpStatusCode.NotFound);
            }
            else
            {
                RespondWithError(context, HttpStatusCode.NotFound);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[{Name}] Error handling request", ex);
            RespondWithError(context, HttpStatusCode.InternalServerError);
        }
    }

    private async Task HandlePostRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var sessionId = context.Request.Headers["Mcp-Session-Id"];
        
        // 解析 JSON-RPC 消息
        var message = await JsonSerializer.DeserializeAsync(
            context.Request.InputStream,
            McpServerRequestJsonContext.Default.JsonRpcRequest,
            cancellationToken);

        if (message is null)
        {
            RespondWithError(context, HttpStatusCode.BadRequest, "Invalid JSON-RPC message");
            return;
        }

        // 对于 initialize 请求，创建新会话
        if (message.Method == "initialize")
        {
            sessionId = Guid.NewGuid().ToString();
            context.Response.Headers.Add("Mcp-Session-Id", sessionId);
        }

        // 将消息放入队列，供业务层处理
        await _messageChannel.Writer.WriteAsync(new TransportMessage
        {
            Message = message,
            SessionId = sessionId,
        }, cancellationToken);

        // 响应 202 Accepted（实际响应由业务层通过 SendMessageAsync 发送）
        context.Response.StatusCode = (int)HttpStatusCode.Accepted;
        context.Response.Close();
    }

    public async Task SendMessageAsync(TransportMessage message, CancellationToken cancellationToken = default)
    {
        // 对于多对一的传输层，必须指定 SessionId
        if (message.SessionId is null)
        {
            throw new InvalidOperationException("SessionId is required for HTTP transport");
        }

        if (!_sessions.TryGetValue(message.SessionId, out var session))
        {
            _logger.Warn($"[{Name}] Session not found: {message.SessionId}");
            return;
        }

        // 通过 SSE 推送消息
        var json = JsonSerializer.Serialize(message.Message, McpServerResponseJsonContext.Default.JsonRpcResponse);
        await session.Writer.WriteAsync($"data: {json}\n\n");
        await session.Writer.FlushAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts.Cancel();
        _listener.Stop();
        _messageChannel.Writer.Complete();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _listener.Close();
        _cts.Dispose();
    }

    private record SseSession(StreamWriter Writer, CancellationTokenSource CancellationToken);
    
    // ... 其他辅助方法（HandleSseConnectionAsync, RespondWithError 等）
}
```

## 使用方式

### 方式 1: 使用 McpServerBuilder（推荐）

```csharp
var server = new McpServerBuilder("MyServer", "1.0.0")
    // 同时支持多种传输层
    .WithStdio()
    .WithHttp(port: 8080, endpoint: "mcp")
    .WithInProcess()
    .WithIpc(pipeName: "my-mcp-server")
    .WithTools(tools => tools
        .AddTool<MyTool>()
    )
    .Build();

await server.RunAsync();
```

### 方式 2: 手动构造传输层

```csharp
var logger = new ConsoleLogger();

// 创建 stdio 传输层
var stdioTransport = new StdioServerTransport(
    new StdioTransportOptions { CaptureStderr = true },
    logger);

// 创建 HTTP 传输层
var httpTransport = new HttpServerTransport(
    new HttpTransportOptions
    {
        BaseUrl = "http://localhost:8080/",
        Endpoint = "mcp",
        SupportLegacyProtocol = true,
    },
    logger);

// 创建服务器
var server = new McpServer
{
    ServerName = "MyServer",
    ServerVersion = "1.0.0",
    Context = new McpServerContext(),
    Transports = new IMcpServerTransport[] { stdioTransport, httpTransport },
    Tools = new McpServerToolsProvider(),
    Resources = new McpServerResourcesProvider(),
};

await server.RunAsync();
```

### 方式 3: 只使用单个传输层

```csharp
// 只用 stdio（最常见的场景）
var server = new McpServerBuilder("MyServer", "1.0.0")
    .WithStdio()
    .WithTools(/* ... */)
    .Build();

await server.RunAsync();
```

### 方式 4: 扩展自定义传输层

```csharp
// 第三方开发者可以实现自己的传输层
public class WebSocketServerTransport : IMcpServerTransport
{
    // 实现接口...
}

// 注册到 Builder（通过扩展方法）
public static class WebSocketTransportExtensions
{
    public static McpServerBuilder WithWebSocket(
        this McpServerBuilder builder,
        WebSocketTransportOptions options)
    {
        builder.AddTransport(new WebSocketServerTransport(options));
        return builder;
    }
}

// 使用
var server = new McpServerBuilder("MyServer", "1.0.0")
    .WithWebSocket(new WebSocketTransportOptions { Port = 9090 })
    .Build();
```

## 扩展指南

### 如何实现自定义传输层？

#### 步骤 1: 创建配置类

```csharp
public sealed record MyTransportOptions : TransportOptions
{
    public required string MyParameter { get; init; }
}
```

#### 步骤 2: 实现 IMcpServerTransport

```csharp
public sealed class MyServerTransport : IMcpServerTransport
{
    public string Name => "my-transport";
    public bool SupportsManyToOne => false; // 根据实际情况
    public bool SupportsServerPush => true;
    public ChannelReader<TransportMessage> MessageReader => _channel.Reader;

    private readonly Channel<TransportMessage> _channel = Channel.CreateUnbounded<TransportMessage>();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 启动监听逻辑
    }

    public async Task SendMessageAsync(TransportMessage message, CancellationToken cancellationToken)
    {
        // 发送消息逻辑
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.Complete();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
```

#### 步骤 3: 创建 Builder 扩展方法

```csharp
public static class MyTransportExtensions
{
    public static McpServerBuilder WithMyTransport(
        this McpServerBuilder builder,
        MyTransportOptions options)
    {
        builder.AddTransport(new MyServerTransport(options));
        return builder;
    }
}
```

#### 步骤 4: 使用

```csharp
var server = new McpServerBuilder("MyServer", "1.0.0")
    .WithMyTransport(new MyTransportOptions { MyParameter = "value" })
    .Build();
```

**完成！一个文件搞定扩展！**

### 扩展的最佳实践

1. **命名约定**：
   - 配置类：`{Transport}TransportOptions`
   - 服务器端：`{Transport}ServerTransport`
   - 客户端：`{Transport}ClientTransport`
   - 扩展方法：`With{Transport}()`

2. **依赖管理**：
   - 传输层实现应该独立，不依赖其他传输层
   - 如果需要共享代码，提取到 `Utils` 或 `Core` 命名空间

3. **日志规范**：
   - 使用 `[{Name}]` 前缀标识传输层
   - 关键事件：`Info` 级别（启动、停止、会话创建/销毁）
   - 详细信息：`Debug` 级别（消息收发）
   - 错误信息：`Error` 级别（异常）

4. **错误处理**：
   - 传输层错误不应导致整个服务器崩溃
   - 使用 `try-catch` 包裹 I/O 操作
   - 记录错误日志并优雅降级

## 迁移计划

### 阶段 1: 接口定义和核心类型（当前阶段）

- [x] 设计并评审传输层接口
- [ ] 创建 `TransportMessage` 类型
- [ ] 创建 `IMcpServerTransport` 和 `IMcpClientTransport` 接口
- [ ] 创建 `TransportOptions` 基类和各传输层配置类

**预计工作量**：1-2 天

### 阶段 2: 重构 HTTP 传输层

- [ ] 将现有 `HttpServerTransport` 重构为实现新接口
- [ ] 提取共享逻辑到辅助类
- [ ] 添加单元测试
- [ ] 更新文档

**预计工作量**：2-3 天

### 阶段 3: 实现 stdio 传输层

- [ ] 实现 `StdioServerTransport`
- [ ] 实现 `StdioClientTransport`
- [ ] 添加集成测试（客户端-服务器对接）
- [ ] 更新文档和示例

**预计工作量**：2-3 天

### 阶段 4: 实现 InProcess 传输层

- [ ] 实现 `InProcessTransport`（同时实现服务器和客户端接口）
- [ ] 添加单元测试
- [ ] 更新文档和示例

**预计工作量**：1-2 天

### 阶段 5: 实现 IPC 传输层

- [ ] 实现 `IpcServerTransport`（基于 Named Pipe）
- [ ] 实现 `IpcClientTransport`
- [ ] 支持单连接和多连接模式
- [ ] 添加跨平台测试（Windows, Linux, macOS）
- [ ] 更新文档和示例

**预计工作量**：3-4 天

### 阶段 6: 更新 McpServerBuilder

- [ ] 重构 `McpServerBuilder` 以支持多传输层
- [ ] 添加 `WithStdio()`, `WithInProcess()`, `WithIpc()` 方法
- [ ] 更新现有的 `WithHttp()` 方法
- [ ] 添加 `AddTransport()` 方法用于扩展

**预计工作量**：1-2 天

### 阶段 7: 文档和示例

- [ ] 更新 Quick Start 文档
- [ ] 添加各传输层的使用示例
- [ ] 添加自定义传输层扩展教程
- [ ] 更新 API 文档

**预计工作量**：2-3 天

**总预计工作量**：12-19 天

## 附录

### A. 设计决策记录

#### 为什么使用 Channel 而非事件？

**决策**：使用 `System.Threading.Channels.Channel` 作为消息传递机制。

**理由**：
- ✅ **背压控制**：Channel 提供了内置的背压机制，防止消息堆积
- ✅ **异步友好**：原生支持 `async/await`，性能优秀
- ✅ **线程安全**：无需手动加锁
- ✅ **可取消**：支持 `CancellationToken`
- ✅ **标准库**：无需额外依赖

**替代方案**：
- ❌ 事件（`event`）：不支持异步，难以控制背压
- ❌ `BlockingCollection`：同步 API，不适合异步场景
- ❌ Rx（`IObservable`）：需要额外依赖，过于复杂

#### 为什么区分 IMcpServerTransport 和 IMcpClientTransport？

**决策**：分为服务器端和客户端两个接口。

**理由**：
- ✅ **职责明确**：服务器负责监听，客户端负责连接
- ✅ **语义清晰**：`StartAsync` vs `ConnectAsync`，`StopAsync` vs `DisconnectAsync`
- ✅ **类型安全**：避免在不适用的场景下调用错误的方法
- ✅ **特殊场景**：InProcess 传输层可以同时实现两个接口

#### 为什么需要 TransportMessage 包装类？

**决策**：不直接传递 `JsonRpcMessage`，而是包装为 `TransportMessage`。

**理由**：
- ✅ **会话管理**：多对一传输层需要 `SessionId`
- ✅ **元数据扩展**：允许传输层附加额外信息（如 HTTP headers）
- ✅ **向后兼容**：未来可以添加新字段而不破坏现有代码

### B. 性能考虑

#### 零拷贝序列化

所有传输层都应使用流式序列化，避免中间字符串分配：

```csharp
// ✅ 推荐：流式序列化
await JsonSerializer.SerializeAsync(stream, message, jsonContext, cancellationToken);

// ❌ 避免：字符串分配
var json = JsonSerializer.Serialize(message, jsonContext);
await writer.WriteAsync(json);
```

#### 对象池

对于高频分配的对象（如 `TransportMessage`），考虑使用对象池：

```csharp
private static readonly ObjectPool<TransportMessage> _messagePool = 
    ObjectPool.Create<TransportMessage>();

var message = _messagePool.Get();
try
{
    message.Message = jsonRpcMessage;
    message.SessionId = sessionId;
    await channel.Writer.WriteAsync(message);
}
finally
{
    _messagePool.Return(message);
}
```

### C. 安全考虑

#### HTTP 传输层安全

- ✅ **验证 Origin**：防止 DNS 重绑定攻击
- ✅ **绑定 localhost**：开发环境只监听 127.0.0.1
- ✅ **HTTPS**：生产环境必须使用 HTTPS
- ✅ **认证**：实现适当的认证机制（API Key, OAuth 等）

#### IPC 传输层安全

- ✅ **ACL 控制**：限制命名管道的访问权限
- ✅ **加密**：敏感数据应加密传输
- ✅ **验证身份**：验证客户端身份（Windows: Impersonation）

### D. 测试策略

#### 单元测试

每个传输层实现都应有独立的单元测试：

```csharp
[TestClass]
public class StdioServerTransportTests
{
    [TestMethod]
    public async Task SendMessage_ShouldWriteToStdout()
    {
        // Arrange
        var transport = new StdioServerTransport(/* ... */);
        
        // Act
        await transport.SendMessageAsync(message);
        
        // Assert
        // 验证 stdout 输出
    }
}
```

#### 集成测试

测试客户端和服务器的端到端通信：

```csharp
[TestMethod]
public async Task ClientServer_ShouldCommunicate()
{
    // Arrange
    var serverTransport = new StdioServerTransport(/* ... */);
    var clientTransport = new StdioClientTransport(/* ... */);
    
    // Act
    await serverTransport.StartAsync();
    await clientTransport.ConnectAsync();
    
    await clientTransport.SendMessageAsync(request);
    var response = await serverTransport.MessageReader.ReadAsync();
    
    // Assert
    Assert.AreEqual(request.Id, response.Message.Id);
}
```

### E. 参考资料

- [MCP 官方规范 - Transports (2025-06-18)](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
- [System.Threading.Channels 官方文档](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels)
- [Named Pipes 官方文档](https://learn.microsoft.com/en-us/dotnet/standard/io/pipe-operations)
- [ASP.NET Core SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr) - 可参考其传输层抽象设计

---

**文档版本**：1.0  
**最后更新**：2025年11月20日  
**作者**：GitHub Copilot + dotnet-campus 团队  
**状态**：✅ 设计方案已完成，待评审和实施
