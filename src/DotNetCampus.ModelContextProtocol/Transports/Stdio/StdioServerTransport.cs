using System.Text;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.Transports.Stdio;

/// <summary>
/// STDIO 传输层，用于通过标准输入输出进行 MCP 通信。
/// </summary>
public class StdioServerTransport : IServerTransport
{
    private readonly IServerTransportManager _manager;

    /// <summary>
    /// 一个控制台传输层永远只会对应唯一的一个会话。
    /// </summary>
    private readonly StdioServerTransportSession _session;

    /// <summary>
    /// 当 STDIO 传输层启用后，此字段会包含用于 MCP 协议传输层输入输出的流。
    /// </summary>
    private StdioProcessInfo? _stdio;

    /// <summary>
    /// 初始化 <see cref="StdioServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    public StdioServerTransport(IServerTransportManager manager)
    {
        _manager = manager;
        _session = new StdioServerTransportSession(manager.Context.Logger);
    }

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public Task<Task> StartAsync(CancellationToken startingCancellationToken, CancellationToken runningCancellationToken)
    {
#if DEBUG
        // System.Diagnostics.Debugger.Launch();
#endif

        Log.Info($"[McpServer][Stdio] Transport started.");

        var utf8 = new UTF8Encoding(false);
        var input = new StreamReader(Console.OpenStandardInput(), utf8);
        var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" };
        _stdio = new StdioProcessInfo
        {
            StandardInput = input,
            StandardOutput = output,
        };
        _session.SetOutput(output);
        _manager.Add(_session);

        return Task.FromResult(RunLoopAsync(runningCancellationToken));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Log.Info($"[McpServer][Stdio] Disposing transport.");

        // 控制台流不应该关闭，因为其他任何代码都可能会用得上。
        _stdio = null;

        return ValueTask.CompletedTask;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        if (_stdio is not { } stdio)
        {
            return;
        }
        var (input, output) = stdio;
        while (!cancellationToken.IsCancellationRequested)
        {
            // 按照 MCP 协议规范对 STDIO 传输层的要求：
            // 消息以换行符分隔，且不得包含嵌入式换行符。
            // Messages are delimited by newlines, and MUST NOT contain embedded newlines.
            var line = await input.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                Log.Info($"[McpServer][Stdio] Client disconnected (end of input stream).");
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            Log.Debug($"[McpServer][Stdio] ← {line}");

            JsonRpcMessage? message;
            try
            {
                message = await _manager.ReadMessageAsync(line);
            }
            catch
            {
                message = null;
            }

            switch (message)
            {
                case JsonRpcResponse response:
                    // 将响应路由到等待的请求。
                    Log.Debug($"[McpServer][Stdio] Routing client response to session.");
                    _session.HandleResponseAsync(response);
                    continue;

                case JsonRpcNotification notification:
                    // 通知，路由到处理器，无需回复。
                    await _manager.HandleRequestAsync(
                        new JsonRpcRequest { Method = notification.Method, Params = notification.Params },
                        s =>
                        {
                            s.AddScoped<IServerTransportSession>(_session);
                            s.AddScoped<IMcpServerSampling>(new McpServerSampling(_session, Log));
                        },
                        cancellationToken);
                    continue;

                case JsonRpcRequest request:
                {
                    var session = _session;
                    var response2 = await _manager.HandleRequestAsync(request,
                        s =>
                        {
                            s.AddScoped<IServerTransportSession>(session);
                            s.AddScoped<IMcpServerSampling>(new McpServerSampling(session, Log));
                        },
                        cancellationToken);
                    if (response2 is null)
                    {
                        // 按照 MCP 协议规范，本次请求仅需响应而无需回复。
                        await output.WriteLineAsync();
                        continue;
                    }
                    await _manager.RespondJsonRpcAsync(output, response2, cancellationToken);
                    continue;
                }

                default:
                    // 无法解析的消息，回复错误。
                    Log.Warn($"[McpServer][Stdio] Received unrecognizable message, responding with error.");
                    await _manager.RespondJsonRpcAsync(output, new JsonRpcResponse
                    {
                        Error = new JsonRpcError
                        {
                            Code = (int)JsonRpcErrorCode.InvalidRequest,
                            Message = $"Invalid request message: {line}",
                        },
                    }, cancellationToken);
                    continue;
            }
        }
    }

    private readonly record struct StdioProcessInfo
    {
        public required StreamReader StandardInput { get; init; }

        public required StreamWriter StandardOutput { get; init; }

        public void Deconstruct(out StreamReader standardInput, out StreamWriter standardOutput)
        {
            standardInput = StandardInput;
            standardOutput = StandardOutput;
        }
    }
}

file static class Extensions
{
    extension(IServerTransportManager manager)
    {
        public async ValueTask RespondJsonRpcAsync(StreamWriter writer, JsonRpcResponse response, CancellationToken cancellationToken)
        {
            try
            {
                await manager.WriteMessageAsync(writer.BaseStream, response, cancellationToken);
                await writer.WriteLineAsync();
            }
            catch
            {
                // 可能目标客户端已退出，重定向的流无法写入。
            }
        }
    }
}
