# McpServerManager

MCP 協定規定一個用戶端只能連接一個 MCP 伺服器。但在智慧型代理人程式中，通常需要同時管理多個 MCP 伺服器（內建工具、外部服務、崗位程式等），並在呼叫時按 {serverName}.{toolName} 格式定位目標工具。

下面是一個用於同時管理多個 MCP 伺服器的管理器範例。

> 本範例遵循 [用戶端"先建立後連接"原則](../knowledge/client-build-before-connect.md)：`McpClientBuilder.Build()` 只建立用戶端物件，不觸發 I/O。註冊階段同步完成，連接階段並行執行。

## 完整程式碼

```csharp
/// <summary>
/// MCP 服务器管理器，管理多个 MCP 服务器连接，生成 {serverName}.{toolName} 形式的暴露名称。
/// </summary>
public sealed class McpServerManager : IAsyncDisposable
{
    private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _connectTimeout;
    private readonly Dictionary<string, McpServerRuntimeState> _servers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, McpToolEndpoint> _tools = new(StringComparer.OrdinalIgnoreCase);

    public McpServerManager(TimeSpan? connectTimeout = null)
    {
        _connectTimeout = connectTimeout ?? DefaultConnectTimeout;
    }

    /// <summary>
    /// 列出所有已注册的服务器（含未连接的），可用于 UI 展示。
    /// </summary>
    public IReadOnlyDictionary<string, McpServerRuntimeState> ListServers()
    {
        return _servers.ToDictionary(x => x.Key, x => x.Value);
    }

    /// <summary>
    /// 列出所有已连接工具的暴露名称。
    /// </summary>
    public IReadOnlyList<string> ListToolNames()
    {
        return _tools.Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ======== 阶段一：注册（同步，无 I/O，立即返回） ========

    /// <summary>
    /// 注册 HTTP MCP 服务器配置（不建立实际连接）。
    /// 连接在 <see cref="ConnectAllAsync"/> 或首次工具调用时惰性触发。
    /// </summary>
    public void AddHttp(
        string serverName, string serverUrl,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        if (_servers.ContainsKey(serverName))
        {
            throw new InvalidOperationException($"MCP 服务器已存在：{serverName}");
        }

        var client = CreateHttpClient(serverName, serverUrl, headers);
        _servers[serverName] = new McpServerRuntimeState(serverName, "http", client);
    }

    /// <summary>
    /// 注册 stdio MCP 服务器配置（不建立实际连接）。
    /// </summary>
    public void AddStdio(
        string serverName,
        string command,
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, string>? env = null)
    {
        if (_servers.ContainsKey(serverName))
        {
            throw new InvalidOperationException($"MCP 服务器已存在：{serverName}");
        }

        var client = new McpClientBuilder($"McpManager/{serverName}", "1.0.0")
            .WithStdio(new StdioClientTransportOptions
            {
                Command = command,
                Arguments = arguments ?? [],
                EnvironmentVariables = env is null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(env, StringComparer.OrdinalIgnoreCase),
            })
            .Build();

        _servers[serverName] = new McpServerRuntimeState(serverName, "stdio", client);
    }

    // ======== 阶段二：连接（并发） ========

    /// <summary>
    /// 并发连接所有已注册但尚未连接的服务器。
    /// 各服务器的连接超时和错误互不影响。
    /// </summary>
    public async Task ConnectAllAsync(CancellationToken cancellation = default)
    {
        var unconnected = _servers.Values.Where(s => !s.IsConnected).ToList();
        if (unconnected.Count == 0)
        {
            return;
        }

        // 并发连接：一个服务器超时不会阻塞其他服务器
        var tasks = unconnected.Select(s => ConnectAndRegisterAsync(s, cancellation));
        await Task.WhenAll(tasks);
    }

    // ======== 调用与查询 ========

    /// <summary>
    /// 调用指定工具。
    /// </summary>
    public async Task<CallToolResult> CallToolAsync(
        string exposedName,
        JsonElement? arguments = null,
        CancellationToken cancellation = default)
    {
        if (!_tools.TryGetValue(exposedName, out var endpoint))
        {
            return CallToolResult.FromError($"未找到工具：{exposedName}");
        }

        return await endpoint.Client.CallToolAsync(endpoint.ToolName, arguments, cancellation);
    }

    // ======== 生命周期 ========

    /// <summary>
    /// 移除 MCP 服务器及其关联的所有工具。
    /// </summary>
    public async Task<bool> RemoveAsync(string serverName)
    {
        if (!_servers.Remove(serverName, out var state))
        {
            return false;
        }

        // 移除该服务器关联的所有工具
        foreach (var key in _tools.Keys.ToList())
        {
            if (ReferenceEquals(_tools[key].Client, state.Client))
            {
                _tools.Remove(key);
            }
        }

        if (state.Client is not null)
        {
            await state.Client.DisposeAsync();
        }

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var state in _servers.Values)
        {
            if (state.Client is not null)
            {
                await state.Client.DisposeAsync();
            }
        }

        _servers.Clear();
        _tools.Clear();
    }

    // ======== 内部实现 ========

    private static McpClient CreateHttpClient(
        string serverName, string url, IReadOnlyDictionary<string, string>? headers)
    {
        var httpClient = new HttpClient();
        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
            }
        }

        return new McpClientBuilder($"McpManager/{serverName}", "1.0.0")
            .WithHttp(new HttpClientTransportOptions
            {
                ServerUrl = url,
                HttpClient = httpClient,
            })
            .Build();
    }

    private async Task ConnectAndRegisterAsync(
        McpServerRuntimeState state, CancellationToken cancellation)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            cts.CancelAfter(_connectTimeout);

            // EnsureConnectedAsync 是惰性连接的入口，Build() 时并未建立连接
            await state.Client.EnsureConnectedAsync(cancellationToken: cts.Token);
            var toolsResult = await state.Client.ListToolsAsync(cancellationToken: cts.Token);
            var tools = toolsResult.Tools;

            foreach (var tool in tools)
            {
                var exposedName = $"{state.Name}.{tool.Name}";
                if (_tools.ContainsKey(exposedName))
                {
                    throw new InvalidOperationException($"工具名冲突：{exposedName}");
                }

                _tools[exposedName] = new McpToolEndpoint(state.Client, tool.Name, exposedName);
            }

            state.IsConnected = true;
            state.ToolCount = tools.Count;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 即使连接失败也记录状态，方便上游查询失败原因
            state.IsConnected = false;
            state.Error = ex.Message;
        }
    }
}

/// <summary>
/// 工具端点，记录一个工具的客户端、原始名称和暴露名称。
/// </summary>
/// <param name="Client">工具所属的 MCP 客户端。</param>
/// <param name="ToolName">工具在 MCP 服务器上的原始名称。</param>
/// <param name="ExposedName">对外暴露的工具名（如 {serverName}.{toolName}）。</param>
public sealed record McpToolEndpoint(McpClient Client, string ToolName, string ExposedName);

/// <summary>
/// MCP 服务器运行时状态（连接状态与工具列表）。
/// </summary>
public sealed class McpServerRuntimeState
{
    public McpServerRuntimeState(string name, string transportType, McpClient client)
    {
        Name = name;
        TransportType = transportType;
        Client = client;
    }

    public string Name { get; }
    public string TransportType { get; }
    public McpClient Client { get; }
    public bool IsConnected { get; set; }
    public string? Error { get; set; }
    public int ToolCount { get; set; }
}
```

## 使用範例

```csharp
await using var manager = new McpServerManager();

// ====== 阶段一：注册（同步，立即返回，无连接） ======
manager.AddHttp(
    "everything",
    "http://localhost:3001/mcp",
    headers: new Dictionary<string, string> { ["Authorization"] = "Bearer xxx" });

manager.AddStdio(
    "python-tools",
    "python",
    arguments: ["-m", "my_mcp_server"],
    env: new Dictionary<string, string> { ["PYTHONPATH"] = "/modules" });

// 此时可以展示已注册的服务器（含未连接的），供 UI 展示
foreach (var (name, state) in manager.ListServers())
{
    Console.WriteLine($"{name}: {(state.IsConnected ? "已连接" : "未连接")}");
}

// ====== 阶段二：并发连接 ======
// 多个服务器同时连接，互不阻塞
await manager.ConnectAllAsync();

// 现在可以列出工具并调用
foreach (var toolName in manager.ListToolNames())
{
    Console.WriteLine(toolName);
}

var result = await manager.CallToolAsync(
    "everything.echo",
    JsonSerializer.SerializeToElement(new { message = "Hello, MCP!" }));

Console.WriteLine(result.Content);
```

## 設計要點

- **註冊與連接分離**：`AddHttp`/`AddStdio` 是同步方法，只儲存組態（無 I/O），連接由 `ConnectAllAsync` 統一處理。這得益於 `McpClientBuilder.Build()` 不觸發連接的設計。
- **並行連接**：`ConnectAllAsync` 透過 `Task.WhenAll` 並行連接所有伺服器。每個伺服器的逾時獨立，一個壞伺服器不會阻塞其他伺服器的連接。
- **UI 友善**：註冊後可立即展示伺服器列表（`ListServers()` 含未連接狀態），無需等待連接完成。
- **錯誤容錯**：單一伺服器連接失敗不擲例外，而是記錄 `state.IsConnected = false` 和 `state.Error`，不影響其他伺服器。
- **O(1) 工具查詢**：`_tools` 字典按暴露名稱索引，`CallToolAsync` 無需走訪。
- **工具名衝突偵測**：加入工具時檢查暴露名稱是否已被其他伺服器佔用。
