# McpServerManager

The MCP protocol specifies that a single client can only connect to one MCP server. However, in agent programs, you typically need to manage multiple MCP servers simultaneously (built-in tools, external services, position programs, etc.), and locate the target tool using the `{serverName}.{toolName}` format when making calls.

Below is a manager example for managing multiple MCP servers simultaneously.

> This example follows the [Client "Build Before Connect" Principle](../knowledge/client-build-before-connect.md): `McpClientBuilder.Build()` only creates client objects and does not trigger I/O. The registration phase completes synchronously, and the connection phase executes concurrently.

## Complete Code

```csharp
/// <summary>
/// MCP server manager that manages connections to multiple MCP servers and generates exposed names in {serverName}.{toolName} format.
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
    /// Lists all registered servers (including unconnected ones), suitable for UI display.
    /// </summary>
    public IReadOnlyDictionary<string, McpServerRuntimeState> ListServers()
    {
        return _servers.ToDictionary(x => x.Key, x => x.Value);
    }

    /// <summary>
    /// Lists the exposed names of all connected tools.
    /// </summary>
    public IReadOnlyList<string> ListToolNames()
    {
        return _tools.Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ======== Phase 1: Registration (synchronous, no I/O, returns immediately) ========

    /// <summary>
    /// Registers an HTTP MCP server configuration (does not establish an actual connection).
    /// The connection is lazily triggered in <see cref="ConnectAllAsync"/> or on the first tool call.
    /// </summary>
    public void AddHttp(
        string serverName, string serverUrl,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        if (_servers.ContainsKey(serverName))
        {
            throw new InvalidOperationException($"MCP server already exists: {serverName}");
        }

        var client = CreateHttpClient(serverName, serverUrl, headers);
        _servers[serverName] = new McpServerRuntimeState(serverName, "http", client);
    }

    /// <summary>
    /// Registers a stdio MCP server configuration (does not establish an actual connection).
    /// </summary>
    public void AddStdio(
        string serverName,
        string command,
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, string>? env = null)
    {
        if (_servers.ContainsKey(serverName))
        {
            throw new InvalidOperationException($"MCP server already exists: {serverName}");
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

    // ======== Phase 2: Connection (concurrent) ========

    /// <summary>
    /// Connects all registered but not yet connected servers concurrently.
    /// The connection timeout and errors for each server do not affect each other.
    /// </summary>
    public async Task ConnectAllAsync(CancellationToken cancellation = default)
    {
        var unconnected = _servers.Values.Where(s => !s.IsConnected).ToList();
        if (unconnected.Count == 0)
        {
            return;
        }

        // Concurrent connection: one server timing out will not block others
        var tasks = unconnected.Select(s => ConnectAndRegisterAsync(s, cancellation));
        await Task.WhenAll(tasks);
    }

    // ======== Invocation and Querying ========

    /// <summary>
    /// Calls the specified tool.
    /// </summary>
    public async Task<CallToolResult> CallToolAsync(
        string exposedName,
        JsonElement? arguments = null,
        CancellationToken cancellation = default)
    {
        if (!_tools.TryGetValue(exposedName, out var endpoint))
        {
            return CallToolResult.FromError($"Tool not found: {exposedName}");
        }

        return await endpoint.Client.CallToolAsync(endpoint.ToolName, arguments, cancellation);
    }

    // ======== Lifecycle ========

    /// <summary>
    /// Removes an MCP server and all its associated tools.
    /// </summary>
    public async Task<bool> RemoveAsync(string serverName)
    {
        if (!_servers.Remove(serverName, out var state))
        {
            return false;
        }

        // Remove all tools associated with this server
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

    // ======== Internal Implementation ========

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

            // EnsureConnectedAsync is the lazy connection entry point; Build() does not establish a connection
            await state.Client.EnsureConnectedAsync(cancellationToken: cts.Token);
            var toolsResult = await state.Client.ListToolsAsync(cancellationToken: cts.Token);
            var tools = toolsResult.Tools;

            foreach (var tool in tools)
            {
                var exposedName = $"{state.Name}.{tool.Name}";
                if (_tools.ContainsKey(exposedName))
                {
                    throw new InvalidOperationException($"Tool name conflict: {exposedName}");
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
            // Record state even on connection failure, so upstream can query the failure reason
            state.IsConnected = false;
            state.Error = ex.Message;
        }
    }
}

/// <summary>
/// Tool endpoint that records a tool's client, original name, and exposed name.
/// </summary>
/// <param name="Client">The MCP client the tool belongs to.</param>
/// <param name="ToolName">The tool's original name on the MCP server.</param>
/// <param name="ExposedName">The externally exposed tool name (e.g. {serverName}.{toolName}).</param>
public sealed record McpToolEndpoint(McpClient Client, string ToolName, string ExposedName);

/// <summary>
/// MCP server runtime state (connection state and tool list).
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

## Usage Example

```csharp
await using var manager = new McpServerManager();

// ====== Phase 1: Registration (synchronous, returns immediately, no connection) ======
manager.AddHttp(
    "everything",
    "http://localhost:3001/mcp",
    headers: new Dictionary<string, string> { ["Authorization"] = "Bearer xxx" });

manager.AddStdio(
    "python-tools",
    "python",
    arguments: ["-m", "my_mcp_server"],
    env: new Dictionary<string, string> { ["PYTHONPATH"] = "/modules" });

// At this point, you can display registered servers (including unconnected ones) for UI purposes
foreach (var (name, state) in manager.ListServers())
{
    Console.WriteLine($"{name}: {(state.IsConnected ? "Connected" : "Not Connected")}");
}

// ====== Phase 2: Concurrent Connection ======
// Multiple servers connect simultaneously without blocking each other
await manager.ConnectAllAsync();

// Now you can list tools and make calls
foreach (var toolName in manager.ListToolNames())
{
    Console.WriteLine(toolName);
}

var result = await manager.CallToolAsync(
    "everything.echo",
    JsonSerializer.SerializeToElement(new { message = "Hello, MCP!" }));

Console.WriteLine(result.Content);
```

## Design Highlights

- **Separation of Registration and Connection**: `AddHttp`/`AddStdio` are synchronous methods that only save configuration (no I/O); connections are handled uniformly by `ConnectAllAsync`. This is made possible by `McpClientBuilder.Build()` not triggering connections.
- **Concurrent Connection**: `ConnectAllAsync` uses `Task.WhenAll` to connect all servers concurrently. Each server's timeout is independent — one broken server will not block connections to others.
- **UI-Friendly**: After registration, the server list can be displayed immediately (`ListServers()` includes unconnected states) without waiting for connections to complete.
- **Error Fault Tolerance**: A single server connection failure does not throw an exception; instead, it records `state.IsConnected = false` and `state.Error`, without affecting other servers.
- **O(1) Tool Lookup**: The `_tools` dictionary is indexed by exposed name; `CallToolAsync` does not require iteration.
- **Tool Name Conflict Detection**: When adding tools, the exposed name is checked for conflicts with names already claimed by other servers.
