using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DotNetCampus.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using McpServerRequestJsonContext = DotNetCampus.ModelContextProtocol.CompilerServices.McpServerRequestJsonContext;
using McpServerResponseJsonContext = DotNetCampus.ModelContextProtocol.CompilerServices.McpServerResponseJsonContext;

namespace DotNetCampus.ModelContextProtocol.Transports.Stdio;

/// <summary>
/// stdio 传输层实现（服务器端）。<br/>
/// 通过 stdin 接收客户端消息，通过 stdout 发送服务器消息。<br/>
/// 消息以换行符分隔，每条消息必须是单行 JSON-RPC 消息。<br/>
/// stdio transport implementation (server side).<br/>
/// Receives client messages via stdin, sends server messages via stdout.<br/>
/// Messages are delimited by newlines, each message must be a single-line JSON-RPC message.
/// </summary>
/// <remarks>
/// 参考 MCP 官方规范：https://modelcontextprotocol.io/specification/2025-06-18/basic/transports#stdio
/// </remarks>
public sealed class StdioServerTransport : IMcpServerTransport
{
    private readonly StdioServerTransportOptions _options;
    private readonly ILogger _logger;
    private readonly Channel<TransportMessageContext> _messageChannel;
    private readonly CancellationTokenSource _cts = new();
    private Task? _readTask;

    /// <summary>
    /// 初始化 <see cref="StdioServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="options">stdio 传输层配置</param>
    /// <param name="logger">日志记录器</param>
    internal StdioServerTransport(StdioServerTransportOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
        _messageChannel = Channel.CreateUnbounded<TransportMessageContext>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <inheritdoc />
    public string Name => _options.Name;

    /// <inheritdoc />
    public ChannelReader<TransportMessageContext> MessageReader => _messageChannel.Reader;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.Info($"[{Name}] Starting stdio transport");

        // 在后台线程读取 stdin
        _readTask = Task.Run(async () =>
        {
            using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

            try
            {
                while (!linkedCts.Token.IsCancellationRequested)
                {
#if NET7_0_OR_GREATER
                    var line = await reader.ReadLineAsync(linkedCts.Token);
#else
                    var line = await reader.ReadLineAsync();
#endif
                    if (line is null)
                    {
                        // EOF - 客户端关闭了连接
                        _logger.Info($"[{Name}] EOF reached, client closed connection");
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        // 忽略空行
                        continue;
                    }

                    try
                    {
                        // 解析 JSON-RPC 消息
                        // stdio 传输层通常接收 JsonRpcRequest，但为了通用性，使用 JsonRpcMessage
                        var request = JsonSerializer.Deserialize(line, McpServerRequestJsonContext.Default.JsonRpcRequest);
                        if (request is not null)
                        {
                            _logger.Trace($"[{Name}] Received message: {request.Method}");
                            var context = new StdioTransportContext();
                            await _messageChannel.Writer.WriteAsync(new TransportMessageContext(request, context), linkedCts.Token);
                        }
                        else
                        {
                            _logger.Warn($"[{Name}] Failed to parse message: {line}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.Error($"[{Name}] JSON parsing error", ex);
                        // 继续处理下一条消息
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
                _logger.Info($"[{Name}] Read task cancelled");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{Name}] Error reading from stdin", ex);
            }
            finally
            {
                _messageChannel.Writer.Complete();
                _logger.Info($"[{Name}] Message channel completed");
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(JsonRpcMessage message, ITransportContext? context = null, CancellationToken cancellationToken = default)
    {
        // stdio 是一对一传输层，不需要 context
        // 将消息序列化为单行 JSON 并写入 stdout
        string json;
        if (message is JsonRpcResponse response)
        {
            json = JsonSerializer.Serialize(response, McpServerResponseJsonContext.Default.JsonRpcResponse);
        }
        else if (message is JsonRpcRequest request)
        {
            json = JsonSerializer.Serialize(request, McpServerRequestJsonContext.Default.JsonRpcRequest);
        }
        else
        {
            throw new ArgumentException($"Unsupported message type: {message.GetType().Name}", nameof(message));
        }

        // 确保原子性：使用 Console.Out 的同步写入避免并发问题
        // 根据 MCP 规范，消息必须以换行符分隔，且不能包含嵌入的换行符
#if NET6_0_OR_GREATER
        await Console.Out.WriteLineAsync(json.AsMemory(), cancellationToken);
#else
        await Console.Out.WriteLineAsync(json);
#endif
#if NET8_0_OR_GREATER
        await Console.Out.FlushAsync(cancellationToken);
#else
        await Console.Out.FlushAsync();
#endif

        _logger.Trace($"[{Name}] Sent message: {message.GetType().Name}");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        _cts.CancelAsync();
#else
        _cts.Cancel();
#endif
        _messageChannel.Writer.Complete();
        _logger.Info($"[{Name}] Stopping stdio transport");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_readTask is not null)
        {
            try
            {
                await _readTask;
            }
            catch
            {
                // 忽略异常
            }
        }
        _cts.Dispose();
        _logger.Info($"[{Name}] Disposed");
    }
}
