using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Stdio;

/// <summary>
/// STDIO 传输层，用于通过标准输入输出进行 MCP 通信。
/// </summary>
public class StdioClientTransport : IClientTransport
{
    private readonly StdioClientTransportOptions _options;
    private readonly IClientTransportManager _manager;

    /// <summary>
    /// 当连接到 STDIO 服务器后，此字段会包含服务器进程信息和用于 MCP 协议传输层输入输出的流。
    /// </summary>
    private StdioProcessInfo? _stdio;

    /// <summary>
    /// 初始化 <see cref="StdioClientTransport"/> 类的新实例。
    /// </summary>
    /// <param name="manager">辅助管理 MCP 传输层的管理器。</param>
    /// <param name="options">STDIO 连接选项。</param>
    public StdioClientTransport(IClientTransportManager manager, StdioClientTransportOptions options)
    {
        _manager = manager;
        _options = options;
    }

    private IMcpLogger Log => _manager.Context.Logger;

    /// <inheritdoc />
    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        Log.Info($"[McpClient][Stdio] Transport started.");

        await DisconnectAsync(cancellationToken);
        var process = await StartProcessAsync();
        if (process is { } stdio)
        {
            _ = RunLoopAsync(stdio, cancellationToken);
        }

        _stdio = process;
    }

    /// <inheritdoc />
    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_stdio is not { } info)
        {
            return ValueTask.CompletedTask;
        }

        _stdio = null;
        return KillProcessAsync(info.Process);
    }

    /// <inheritdoc />
    public async ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        if (_stdio is not { } stdio)
        {
            return;
        }

        var line = _manager.WriteMessageAsync(message);
        Log.Debug($"[McpClient][Stdio] → {line}");
        await stdio.StandardInput.WriteAsync(line);
        await stdio.StandardInput.WriteAsync('\n');
        await stdio.StandardInput.FlushAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_stdio is not { } info)
        {
            return;
        }

        _stdio = null;
        await KillProcessAsync(info.Process);
    }

    private async Task RunLoopAsync(StdioProcessInfo stdio, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // 按照 MCP 协议规范对 STDIO 传输层的要求：
            // 消息以换行符分隔，且不得包含嵌入式换行符。
            // Messages are delimited by newlines, and MUST NOT contain embedded newlines.
            var line = await stdio.StandardOutput.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            Log.Debug($"[McpClient][Stdio] ← {line}");

            // 一次解析即可分类：有 method → 服务器主动请求；有 result/error → 对客户端请求的响应。
            JsonRpcMessage? message;
            try
            {
                message = await _manager.ReadMessageAsync(line);
            }
            catch
            {
                Log.Warn($"[McpClient][Stdio] Invalid server message received.");
                continue;
            }

            switch (message)
            {
                case JsonRpcRequest request:
                    await _manager.HandleServerRequestAsync(request, cancellationToken);
                    break;
                case JsonRpcResponse response:
                    await _manager.HandleRespondAsync(response, cancellationToken);
                    break;
                default:
                    Log.Warn($"[McpClient][Stdio] Unrecognized server message received.");
                    break;
            }
        }
    }

    [Pure]
    private Task<StdioProcessInfo?> StartProcessAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                return StartProcessCore(_options);
            }
            catch (Exception ex)
            {
                Log.Error($"[McpClient][Stdio] Failed to start STDIO process.", ex);
                return null;
            }
        });

        static StdioProcessInfo? StartProcessCore(StdioClientTransportOptions options)
        {
            // 解析命令路径，支持命令名（如 npx）和完整路径
            var commandPath = McpStdioUtils.ResolveCommandPath(options.Command);
            if (commandPath is null)
            {
                throw new FileNotFoundException($"无法找到命令：{options.Command}");
            }

            var utf8 = new UTF8Encoding(false);
            var startInfo = new ProcessStartInfo(commandPath)
            {
                CreateNoWindow = true,
                ErrorDialog = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = utf8,
                StandardInputEncoding = utf8,
                StandardOutputEncoding = utf8,
                UseShellExecute = false,
            };
            foreach (var argument in options.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
            foreach (var environmentVariable in options.EnvironmentVariables)
            {
                startInfo.EnvironmentVariables[environmentVariable.Key] = environmentVariable.Value;
            }
            var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }
            return new StdioProcessInfo
            {
                Process = process,
                StandardInput = process.StandardInput,
                StandardOutput = process.StandardOutput,
                StandardError = process.StandardError,
            };
        }
    }

    private async ValueTask KillProcessAsync(Process process)
    {
        await Task.Run(() =>
        {
            try
            {
                process.Kill(true);
            }
            catch (Exception ex)
            {
                Log.Error($"[McpClient][Stdio] Failed to stop STDIO process.", ex);
            }
        });
    }

    private readonly record struct StdioProcessInfo
    {
        public required Process Process { get; init; }

        public required StreamWriter StandardInput { get; init; }

        public required StreamReader StandardOutput { get; init; }

        public required StreamReader StandardError { get; init; }
    }
}
