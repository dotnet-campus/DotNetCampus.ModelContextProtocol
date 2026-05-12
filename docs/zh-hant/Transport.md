# Transport

MCP 傳輸層只負責收發 JSON-RPC 訊息。業務程式碼通常只需要在伺服器端和用戶端選擇同一種傳輸層。

## 速覽

| 傳輸層           | 核心庫內建 | 接聽位址            | 適用情節           |
| ---------------- | ---------- | ------------------- | ------------------ |
| HTTP（內建）     | ✅          | 僅限 `localhost`    | 本機開發、單機部署 |
| TouchSocket HTTP | ❌ 需擴充   | 可接聽 `0.0.0.0` 等 | 區域網路/公開網路  |
| stdio            | ✅          | -                   | 用戶端啟動伺服器處理序 |
| In-Process       | ✅          | -                   | 同處理序嵌入、整合測試 |
| IPC              | ❌ 需擴充   | -                   | 同機跨處理序高速通訊 |

> 核心庫內建的 HTTP 傳輸層僅接聽本機回環位址，是出於安全和盡可能減少引入相依性的考量。如果需要接聽 `0.0.0.0` 等非回環位址，請使用 [TouchSocket HTTP](#touchsocket-http擴充)；如果需要 IPC 傳輸層，請使用 [IPC](#ipc)。這兩種傳輸層的具體取得方式見 [擴充傳輸層的兩種取得方式](#擴充傳輸層的兩種取得方式)。

---

我們假設你在閱讀本文前，已經完成了 [快速開始](QuickStart.md) 中 MCP 伺服器和用戶端的建置。

> **核心原則**：`McpClientBuilder.Build()` 只建立用戶端物件，**不觸發任何 I/O 或網路連接**。連接在首次 API 呼叫時由 `EnsureConnectedAsync` 惰性觸發。詳細說明見 [用戶端"先建立後連接"原則](../knowledge/client-build-before-connect.md)。

## HTTP

[快速開始](QuickStart.md) 使用的就是 Streamable HTTP 傳輸層。

伺服器端：

```csharp
var mcpServer = new McpServerBuilder("示例伺服器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    // 接聽 http://localhost:5943/mcp
    .WithLocalHostHttp(5943, "mcp")
    .Build();
```

用戶端：

```csharp
// 簡單多載：僅指定 URL
var mcpClient = new McpClientBuilder("示例用戶端", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

// 選項多載：可設定自訂 HttpClient（驗證標頭、代理等）
var mcpClient = new McpClientBuilder("示例用戶端", "1.0.0")
    .WithHttp(new HttpClientTransportOptions
    {
        ServerUrl = "http://localhost:5943/mcp",
        HttpClient = customHttpClient,
    })
    .Build();
```

> 本程式庫內建的 HTTP 伺服器端傳輸層（`LocalHostHttpServerTransport`）僅接聽 `127.0.0.1` 和 `[::1]`。如果你需要接聽其他位址（如 `0.0.0.0`），請參考下方 [TouchSocket HTTP（擴充）](#touchsocket-http擴充) 章節。

---

## TouchSocket HTTP（擴充）

核心庫內建的 HTTP 傳輸層僅接聽本機回環位址。如果你需要接聽 `0.0.0.0` 等非回環位址（例如部署到區域網路或公開網路），可以使用 TouchSocket HTTP 傳輸層。

TouchSocket HTTP 傳輸層有兩種取得方式，詳見 [擴充傳輸層的兩種取得方式](#擴充傳輸層的兩種取得方式)。以下範例假設你已透過任一方式獲得 TouchSocket HTTP 傳輸層支援：

### 伺服器端

```csharp
// 簡單多載：接聽本機 localhost
var mcpServer = new McpServerBuilder("示例伺服器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithTouchSocketHttp(5943, "mcp")
    .Build();

// 接聽 0.0.0.0（所有網路介面，包括區域網路和公開網路）
var mcpServer = new McpServerBuilder("公開網路伺服器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithTouchSocketHttp(["0.0.0.0:5943", "[::]:5943"], "mcp")
    .Build();

// 選項多載：可設定全部參數
var mcpServer = new McpServerBuilder("示例伺服器", "1.0.0")
    .WithTools(t => t.WithTool(() => new SampleTools()))
    .WithTouchSocketHttp(new TouchSocketHttpServerTransportOptions
    {
        Listen = ["0.0.0.0:5943", "[::]:5943"],
        EndPoint = "mcp",
    })
    .Build();
```

`Listen` 清單使用 `"IP:埠號"` 格式，只能使用 IP 位址，不能使用網域名稱。可同時接聽多個位址和埠號。

### 複用已有的 HttpService

如果你已經有正在執行的 `HttpService`（TouchSocket 的核心型別），可以將 MCP 伺服器端作為外掛掛載上去：

```csharp
// httpService 是你已有的 HttpService 實例
httpService.UseMcpServer("示例伺服器", "1.0.0", builder =>
{
    builder.WithTools(t => t.WithTool(() => new SampleTools()));
});

// 也可以指定自訂端點
httpService.UseMcpServer("示例伺服器", "1.0.0", "/custom-mcp", builder =>
{
    builder.WithTools(t => t.WithTool(() => new SampleTools()));
});
```

> `HttpService` 實作了 `IPluginManager` 介面，`UseMcpServer` 是 `IPluginManager` 的擴充方法。

### 用戶端

TouchSocket HTTP 傳輸層的用戶端無需特殊處理——用戶端只需向伺服器端發起 HTTP 請求，可直接複用核心庫的 HTTP 用戶端傳輸層：

```csharp
var mcpClient = new McpClientBuilder("示例用戶端", "1.0.0")
    .WithHttp("http://192.168.1.100:5943/mcp")
    .Build();
```

---

## stdio

stdio 適合由用戶端啟動伺服器端處理序的情節，也是 MCP 協定建議伺服器支援的傳輸層。

伺服器端：

```csharp
var mcpServer = new McpServerBuilder("示例伺服器", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // 透過標準輸入輸出收發 MCP 訊息
    .WithStdio()
    .Build();

await mcpServer.RunAsync();
```

用戶端：

```csharp
// 簡單多載：指定命令和參數
var mcpClient = new McpClientBuilder("示例用戶端", "1.0.0")
    // 用戶端會啟動此命令，並透過該處理序的標準輸入輸出通訊
    .WithStdio("dotnet", ["run", "--project", "../MinimalMcpServer"])
    .Build();

// 選項多載：可設定環境變數
var mcpClient = new McpClientBuilder("示例用戶端", "1.0.0")
    .WithStdio(new StdioClientTransportOptions
    {
        Command = "dotnet",
        Arguments = ["run", "--project", "../MinimalMcpServer"],
        EnvironmentVariables = new Dictionary<string, string>
        {
            ["DOTNET_ENVIRONMENT"] = "Production",
        },
    })
    .Build();
```

## In-Process

In-Process 適合同處理序嵌入和整合測試。伺服器端需要先啟動，用戶端第一次要求時會建立連接。

```csharp
var mcpServer = new McpServerBuilder("內嵌伺服器", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // 允許同處理序內的 MCP 用戶端連接
    .WithInProcess()
    .Build();

await mcpServer.StartAsync();
try
{
    await using var mcpClient = new McpClientBuilder("內嵌用戶端", "1.0.0")
        .WithInProcess(mcpServer)
        .Build();

    var arguments = JsonSerializer.SerializeToElement(new { text = "Hello" });
    var result = await mcpClient.CallToolAsync("echo_tool", arguments);

    Console.WriteLine(result.Content);
}
finally
{
    await mcpServer.StopAsync();
}
```

In-Process 傳輸層不提供處理序隔離，伺服器端與用戶端執行在同一處理序和權限下，適合可信邊界內的嵌入式情節和整合測試。

> `SampleTools` 的定義見 [Tools - 簡單範例](Tools.md#簡單範例)。

## IPC

IPC 傳輸層適合同一台機器上不同處理序之間通訊，基於 dotnetCampus.Ipc 提供的命名管道實作。

IPC 傳輸層有兩種取得方式，詳見 [擴充傳輸層的兩種取得方式](#擴充傳輸層的兩種取得方式)。以下範例假設你已透過任一方式獲得 IPC 傳輸層支援：

### 伺服器端

```csharp
var mcpServer = new McpServerBuilder("IPC 示例伺服器", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    // sample-mcp-pipe 是本機管道名，用戶端需要使用同一個名字連接
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();

await mcpServer.RunAsync();
```

也可以複用外部建立的 `IpcProvider`：

```csharp
var mcpServer = new McpServerBuilder("IPC 示例伺服器", "1.0.0")
    .WithTools(tools => tools.WithTool(() => new SampleTools()))
    .WithDotNetCampusIpc(existingIpcProvider)
    .Build();
```

### 用戶端

```csharp
await using var mcpClient = new McpClientBuilder("IPC 示例用戶端", "1.0.0")
    .WithDotNetCampusIpc("sample-mcp-pipe")
    .Build();
```

也可以複用外部建立的 `IpcProvider`：

```csharp
await using var mcpClient = new McpClientBuilder("IPC 示例用戶端", "1.0.0")
    .WithDotNetCampusIpc(existingIpcProvider, "sample-mcp-pipe")
    .Build();
```

---

## 擴充傳輸層的兩種取得方式

核心庫僅內建了 HTTP（localhost）、stdio 和 In-Process 三種傳輸層。如果需要 TouchSocket HTTP 或 IPC 傳輸層，可以透過以下兩種方式取得：

### 方式一：安裝擴充套件包（推薦）

直接安裝對應的擴充 NuGet 套件：

```bash
# TouchSocket HTTP 傳輸層
dotnet add package DotNetCampus.ModelContextProtocol.TouchSocket.Http

# IPC 傳輸層
dotnet add package DotNetCampus.ModelContextProtocol.Ipc
```

安裝後即可直接使用 `.WithTouchSocketHttp()` / `.WithDotNetCampusIpc()` 等擴充方法。擴充套件包已將底層相依性（`TouchSocket.Http` / `dotnetCampus.Ipc`）一併引入，這是最簡單的方式。

### 方式二：自行安裝底層庫 + 啟用原始碼產生器

如果你希望盡可能減少專案中引入的 `.dll` 檔案數量（我就喜歡這麼幹），可以不安裝擴充套件包，而是自行安裝底層庫，並啟用原始碼產生器自動生成傳輸層程式碼：

```bash
# 安裝底層庫（而非擴充套件包）
dotnet add package dotnetCampus.Ipc
dotnet add package TouchSocket.Http
```

然後在專案 `.csproj` 中開啟原始碼產生器：

```xml
<PropertyGroup>
  <DotNetCampusModelContextProtocolGenerateTransports>true</DotNetCampusModelContextProtocolGenerateTransports>
</PropertyGroup>
```

開啟此選項後，`DotNetCampus.ModelContextProtocol` 核心套件附帶的分析器（Analyzer）會自動掃描專案中已安裝的庫：

| 偵測到的庫         | 自動生成的傳輸層        |
| ------------------ | ----------------------- |
| `dotnetCampus.Ipc` | IPC 傳輸層              |
| `TouchSocket.Http` | TouchSocket HTTP 傳輸層 |

**未安裝的庫不會生成任何程式碼**——原始碼產生器是安全的，不會汙染專案。

> **設計理念**：dotnet-campus 組織傾向於保持核心庫的零相依性和輕量化。IPC 和 TouchSocket HTTP 作為可選的擴充傳輸層，不會被強行塞入核心庫。開發者可以根據實際需要自取所需的傳輸層，而不會被強制引入不需要的相依性。

---

## 自訂傳輸層

用戶端傳輸層實作 `IClientTransport`，並透過 `IClientTransportManager` 完成 JSON-RPC 序列化和回應分派。

```csharp
public sealed class MyClientTransport(IClientTransportManager manager) : IClientTransport
{
    public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        // 在這裡連接到你的底層通道。
        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        // 在這裡關閉底層通道。
        return ValueTask.CompletedTask;
    }

    public async ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        // 將 JSON-RPC 訊息序列化成字串，然後發送到底層通道。
        var line = manager.WriteMessageAsync(message);
        await SendLineAsync(line, cancellationToken);
    }

    public ValueTask DisposeAsync() => DisconnectAsync();

    private async Task OnLineReceivedAsync(string line, CancellationToken cancellationToken)
    {
        // 底層通道收到伺服器端訊息後，反序列化並交回 MCP 用戶端處理。
        var message = await manager.ReadMessageAsync(line);
        switch (message)
        {
            case JsonRpcResponse response:
                await manager.HandleRespondAsync(response, cancellationToken);
                break;

            case JsonRpcRequest request:
                await manager.HandleServerRequestAsync(request, cancellationToken);
                break;
        }
    }

    private static ValueTask SendLineAsync(string line, CancellationToken cancellationToken)
    {
        // 把 line 寫入你的底層通道。
        return ValueTask.CompletedTask;
    }
}
```

註冊到用戶端：

```csharp
var mcpClient = new McpClientBuilder("自訂傳輸層用戶端", "1.0.0")
    .WithTransport(manager => new MyClientTransport(manager))
    .Build();
```

伺服器端傳輸層實作 `IServerTransport`：

```csharp
public sealed class MyServerTransport(IServerTransportManager manager) : IServerTransport
{
    public Task<Task> StartAsync(CancellationToken startingCancellationToken, CancellationToken runningCancellationToken)
    {
        // 第一層 Task 完成表示傳輸層已啟動，傳回的第二層 Task 在傳輸層停止時完成。
        return Task.FromResult(RunAsync(runningCancellationToken));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task OnLineReceivedAsync(string line, Stream responseStream, CancellationToken cancellationToken)
    {
        var message = await manager.ReadMessageAsync(line);
        if (message is not JsonRpcRequest request)
        {
            return;
        }

        // 將用戶端要求交給 MCP 伺服器端處理。
        var response = await manager.HandleRequestAsync(request, cancellationToken: cancellationToken);
        if (response is not null)
        {
            await manager.WriteMessageAsync(responseStream, response, cancellationToken);
        }
    }

    private static async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
```

註冊到伺服器端：

```csharp
var mcpServer = new McpServerBuilder("自訂傳輸層伺服器", "1.0.0")
    .WithTransport(manager => new MyServerTransport(manager))
    .Build();
```

如果你的伺服器端傳輸層需要支援伺服器主動向用戶端發起要求，例如 Sampling，還需要為每個用戶端連接實作 `IServerTransportSession`。

> **建構原則**：用戶端傳輸層建構函式**只儲存參數，不執行 I/O**。所有連接工作（啟動處理序、建立網路連接等）在 `ConnectAsync` 中完成，且 `ConnectAsync` 必須冪等。詳見 [用戶端"先建立後連接"原則](../knowledge/client-build-before-connect.md)。
