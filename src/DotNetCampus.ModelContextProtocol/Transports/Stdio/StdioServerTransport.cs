using System.Text;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

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
    private (StreamReader Input, StreamWriter Output)? _consoleStreams;

    /// <summary>
    /// 初始化 <see cref="StdioServerTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    public StdioServerTransport(IServerTransportManager manager)
    {
        _manager = manager;
        _session = new StdioServerTransportSession();
    }

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public Task<Task> StartAsync(CancellationToken cancellationToken = default)
    {
#if DEBUG
        // System.Diagnostics.Debugger.Launch();
#endif

        Log.Info($"[McpServer][Stdio] Starting STDIO server transport.");

        var utf8 = new UTF8Encoding(false);
        var input = new StreamReader(Console.OpenStandardInput(), utf8);
        var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" };
        _consoleStreams = (input, output);
        _manager.Add(_session);

        return Task.FromResult(RunLoopAsync(cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Log.Info($"Disposing StdioServerTransport");

        // 控制台流不应该关闭，因为其他任何代码都可能会用得上。
        _consoleStreams = null;

        return ValueTask.CompletedTask;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        if (_consoleStreams is not { } streams)
        {
            return;
        }
        var (input, output) = streams;
        while (!cancellationToken.IsCancellationRequested)
        {
            // 按照 MCP 协议规范对 STDIO 传输层的要求：
            // 消息以换行符分隔，且不得包含嵌入式换行符。
            // Messages are delimited by newlines, and MUST NOT contain embedded newlines.
            var line = await input.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            var request = await _manager.ParseAndCatchRequestAsync(line);
            if (request is null)
            {
                await output.RespondJsonRpcAsync(new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = (int)JsonRpcErrorCode.InvalidRequest,
                        Message = $"Invalid request message: {line}",
                    },
                }, cancellationToken);
                continue;
            }

            var response = await _manager.HandleRequestAsync(request, null, cancellationToken);
            if (response is null)
            {
                // 按照 MCP 协议规范，本次请求仅需响应而无需回复。
                await output.WriteLineAsync();
                continue;
            }

            await output.RespondJsonRpcAsync(response, cancellationToken);
        }
    }
}

file static class Extensions
{
    public static async ValueTask<JsonRpcRequest?> ParseAndCatchRequestAsync(this IServerTransportManager manager, string inputMessageText)
    {
        try
        {
            return await manager.ParseRequestAsync(inputMessageText);
        }
        catch
        {
            // 请求消息格式不正确，返回 null 后，原样给 MCP 客户端报告错误。
            return null;
        }
    }

    public static async ValueTask RespondJsonRpcAsync(this StreamWriter writer, JsonRpcResponse response, CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(writer.BaseStream, response, McpServerResponseJsonContext.Default.JsonRpcResponse, cancellationToken);
        await writer.WriteLineAsync();
    }
}
