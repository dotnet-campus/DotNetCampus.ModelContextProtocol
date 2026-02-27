namespace DotNetCampus.ModelContextProtocol.Transports.Stdio;

/// <summary>
/// 提供给 STDIO 传输层使用的辅助方法。
/// </summary>
public static class McpStdioUtils
{
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

    /// <summary>
    /// 解析当前系统环境下某个命令的完整运行路径。
    /// </summary>
    /// <param name="command">命令，例如 npx。</param>
    /// <returns>命令的完整运行路径，如果无法解析，则返回 <see langword="null"/>。</returns>
    public static string? ResolveCommandPath(string command)
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
