using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Hosting.Services;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Servers;
using DotNetCampus.ModelContextProtocol.Utils;

namespace DotNetCampus.ModelContextProtocol.Transports;

internal class ServerTransportManager(McpServer server, McpServerContext context) : IServerTransportManager
{
    /// <summary>
    /// 表示 MCP 服务正在运行的 <see cref="CancellationTokenSource"/>。
    /// </summary>
    private readonly CancellationTokenSource _runningCancellationTokenSource = new();

    /// <summary>
    /// 已注册的传输层集合。
    /// </summary>
    private readonly List<IServerTransport> _transports = [];

    /// <summary>
    /// 当前正在使用的传输层会话集合。
    /// </summary>
    private readonly ConcurrentDictionary<string, IServerTransportSession> _sessions = [];

    /// <summary>
    /// 每次建立连接后，将注册一个候选的传输层会话工厂；后续处理请求时，会使用这些工厂创建传输层会话。
    /// </summary>
    private readonly ConcurrentDictionary<Type, Func<string, IServerTransportSession>> _candidateSessions = [];

    /// <summary>
    /// 桥接 MCP 协议的传输层与 MCP 协议的应用层。
    /// </summary>
    private readonly McpProtocolBridge _bridge = new(context);

    /// <summary>
    /// 获取或初始化服务器名称。
    /// </summary>
    public string ServerName => server.ServerName;

    /// <summary>
    /// 获取或初始化服务器版本。
    /// </summary>
    public string ServerVersion => server.ServerVersion;

    /// <inheritdoc />
    public IServerTransportContext Context => context;

    /// <summary>
    /// 获取已注册的传输层列表。
    /// </summary>
    public IReadOnlyList<IServerTransport> Transports => _transports;

    public bool Add(IServerTransport transport)
    {
        if (_transports.Contains(transport))
        {
            return false;
        }
        _transports.Add(transport);
        return true;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _runningCancellationTokenSource.Token);
        var runTasks = _transports
            .Select(t => t.StartAsync(source.Token, source.Token).Unwrap())
            .ToList();
        await Task.WhenAll(runTasks);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var source = _runningCancellationTokenSource;
        var startTasks = _transports
            .Select(t => t.StartAsync(cancellationToken, source.Token))
            .ToList();
        await Task.WhenAll(startTasks);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        await _runningCancellationTokenSource.CancelAsync();
#else
        await Task.Yield();
        _runningCancellationTokenSource.Cancel();
#endif
    }

    public SessionId MakeNewSessionId()
    {
        var sessionId = SessionId.MakeNew();
        while (_sessions.ContainsKey(sessionId.Id))
        {
            sessionId = SessionId.MakeNew();
        }
        return sessionId;
    }

    public void Add(IServerTransportSession session)
    {
        // 多对一的传输层会话。
        if (session.SessionId is { } sessionId)
        {
            var isAdded = _sessions.TryAdd(sessionId, session);
            if (!isAdded)
            {
                throw new InvalidOperationException($"Session ID '{sessionId}' already exists.");
            }
            return;
        }

        // 一对一的传输层会话。
        var noUseSessionId = MakeNewSessionId();
        _sessions.AddOrUpdate(noUseSessionId.Id, session, (_, _) => session);
    }

    public bool TryGetSession<T>(string sessionId, [NotNullWhen(true)] out T? session) where T : class, IServerTransportSession
    {
        if (_sessions.TryGetValue(sessionId, out var baseSession) && baseSession is T typedSession)
        {
            session = typedSession;
            return true;
        }

        session = null;
        return false;
    }

    public ValueTask<JsonRpcRequest?> ReadRequestAsync(string requestLine)
    {
        var message = JsonSerializer.Deserialize(requestLine, McpServerRequestJsonContext.Default.JsonRpcRequest);
        if (message is { Method: RequestMethods.Initialize, Id: null })
        {
            return ValueTask.FromResult<JsonRpcRequest?>(message with { Id = MakeNewSessionId().ToJsonElement() });
        }
        return ValueTask.FromResult<JsonRpcRequest?>(message);
    }

    public async ValueTask<JsonRpcRequest?> ReadRequestAsync(Stream requestStream)
    {
        var message = await JsonSerializer.DeserializeAsync(requestStream, McpServerRequestJsonContext.Default.JsonRpcRequest);
        if (message is { Method: RequestMethods.Initialize, Id: null })
        {
            return message with { Id = MakeNewSessionId().ToJsonElement() };
        }
        return message;
    }

    public async ValueTask<JsonRpcRequest?> ReadRequestAsync(ReadOnlyMemory<byte> requestMemory)
    {
        var pipeReader = PipeReader.Create(new ReadOnlySequence<byte>(requestMemory));
        var message = await JsonSerializer.DeserializeAsync(pipeReader, McpServerRequestJsonContext.Default.JsonRpcRequest);
        if (message is { Method: RequestMethods.Initialize, Id: null })
        {
            return message with { Id = MakeNewSessionId().ToJsonElement() };
        }
        return message;
    }

    public Task WriteMessageAsync(Stream stream, JsonRpcMessage message, CancellationToken cancellationToken) => message switch
    {
        JsonRpcResponse response => JsonSerializer.SerializeAsync(stream, response,
            McpServerResponseJsonContext.Default.JsonRpcResponse, cancellationToken),
        JsonRpcRequest request => JsonSerializer.SerializeAsync(stream, request,
            McpServerRequestJsonContext.Default.JsonRpcRequest, cancellationToken),
        JsonRpcNotification notification => JsonSerializer.SerializeAsync(stream, notification,
            McpServerRequestJsonContext.Default.JsonRpcNotification, cancellationToken),
        _ => throw new InvalidOperationException($"Unsupported message type: {message.GetType().FullName}"),
    };

    public ValueTask<JsonRpcResponse?> HandleRequestAsync(JsonRpcRequest? request,
        Action<IMcpServiceCollection>? additionalServices = null, CancellationToken cancellationToken = default)
    {
        var services = new ScopedServiceProvider(context.ServiceProvider);
        additionalServices?.Invoke(services);
        return _bridge.HandleRequestAsync(services, request, cancellationToken);
    }
}
