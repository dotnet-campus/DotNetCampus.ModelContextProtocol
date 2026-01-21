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
        Log.Info($"[McpClient][Stdio] Starting STDIO client transport.");

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

        var line = _manager.WriteRequestAsync(message);
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

            var response = await _manager.ParseAndCatchResponseAsync(line);
            if (response is null)
            {
                Log.Warn($"Invalid server message: {line}");
                continue;
            }

            await _manager.HandleRespondAsync(response, cancellationToken);
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
                Log.Error($"STDIO 服务器启动失败。", ex);
                return null;
            }
        });

        static StdioProcessInfo? StartProcessCore(StdioClientTransportOptions options)
        {
            // 解析命令路径，支持命令名（如 npx）和完整路径
            var commandPath = ResolveCommandPath(options.Command);
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

        static string? ResolveCommandPath(string command)
        {
            // 如果命令包含路径分隔符，说明是路径而非命令名
            if (command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
            {
                return File.Exists(command) ? Path.GetFullPath(command) : null;
            }

            // 在 PATH 环境变量中查找命令
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            // 尝试提取命令里原本的扩展名（例如用户可能指定的是 npx/dnx 也可能指定的是 npx.cmd/dnx.exe。
            var extensionInCommand = Path.GetExtension(command);
            var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            var extensions = ExecutableExtensionsLazy.Value;

            foreach (var path in paths)
            {
                if (extensions.Count is 0)
                {
                    // Linux 等无扩展名的可执行程序。
                    var fullPath = Path.Join(path, command);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                    continue;
                }

                // Windows 等带扩展名的可执行程序。
                foreach (var extension in extensions)
                {
                    var fullPath = extensionInCommand?.Equals(extension, StringComparison.OrdinalIgnoreCase) is true
                        // 如果命令自带的扩展名正好与环境变量里的相同，说明命令本身确实带的是扩展名。
                        ? Path.Join(path, command)
                        // 否则，叠加环境变量里的扩展名。
                        : Path.Join(path, command + extension);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            return null;
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
                Log.Error($"STDIO 服务器关闭失败。", ex);
            }
        });
    }

    private static readonly Lazy<IReadOnlyList<string>> ExecutableExtensionsLazy = new Lazy<IReadOnlyList<string>>(() =>
    {
        if (!OperatingSystem.IsWindows())
        {
            // Unix 系统上可执行文件通常没有扩展名
            return [""];
        }
        // Windows 上从 PATHEXT 环境变量获取可执行扩展名
        var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrEmpty(pathExt))
        {
            return [".exe", ".cmd", ".bat", ".com"];
        }
        var extensions = pathExt.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        // 确保包含空扩展名（用于无扩展名的可执行文件）
        return extensions;
        // 默认 Windows 可执行扩展名
    }, LazyThreadSafetyMode.None);

    private readonly record struct StdioProcessInfo
    {
        public required Process Process { get; init; }

        public required StreamWriter StandardInput { get; init; }

        public required StreamReader StandardOutput { get; init; }

        public required StreamReader StandardError { get; init; }
    }
}

file static class Extensions
{
    extension(IClientTransportManager manager)
    {
        public async ValueTask<JsonRpcResponse?> ParseAndCatchResponseAsync(string inputMessageText)
        {
            try
            {
                return await manager.ReadResponseAsync(inputMessageText);
            }
            catch
            {
                // 响应消息格式不正确，返回 null 后，原样给 MCP 客户端报告错误。
                return null;
            }
        }
    }
}
