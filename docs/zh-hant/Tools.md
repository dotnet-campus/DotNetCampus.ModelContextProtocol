# Tools

Tools 用於讓用戶端要求伺服器端執行動作。我們假設你在閱讀本文前，已經完成了 [快速開始](QuickStart.md) 中 MCP 伺服器和用戶端的建置。

## 伺服器端實作工具

### 簡單範例

一個簡單的 MCP 工具實作如下：

```csharp
public class SampleTools
{
    /// <summary>
    /// 用於給 AI 偵錯使用的工具，原樣傳回一些資訊
    /// </summary>
    /// <param name="text">要原樣傳回的字串</param>
    /// <returns>原樣傳回的字串</returns>
    [McpServerTool(ReadOnly = true)]
    public string EchoTool(string text)
    {
        return text;
    }
}
```

在這個範例中：

- 註解會成為此工具描述的重要部分，並在 MCP 協定中傳送給用戶端，所以寫好註解對大語言模型正確使用工具非常重要；另外，不用太在意註解的多語言問題，因為大語言模型不在乎你用什麼語言描述工具
- 如果參數是複雜的資料型別或列舉，不必在參數註解中詳細描述每個內部屬性或欄位，因為本程式庫會自動遞迴地擷取註解，並將它們包含在 MCP 協定中傳送給用戶端
- 工具的名稱預設使用 snake_case 命名法轉換方法名，本例中，你會得到 `echo_tool`

### 自訂工具屬性

`[McpServerTool]` 特性支援多個屬性，讓你精細控制工具在 MCP 協定中的行為和中繼資料：

```csharp
/// <summary>
/// 用於給 AI 偵錯使用的工具，原樣傳回一些資訊
/// </summary>
/// <param name="text">要原樣傳回的字串</param>
[McpServerTool(
    Name = "echo_tool",          // 指定工具名稱，避免方法名與工具名強繫結（例如避免 Async 後綴影響工具名）
    Title = "原樣輸出",          // 給人類閱讀的工具名稱，AI 看不到，可用於 UI 展示
    Description = "用於給 AI 偵錯使用的工具，原樣傳回一些資訊",  // 覆蓋方法註解中的描述
    Idempotent = true,           // 標記為冪等工具，用戶端可以安全地重試呼叫
    OpenWorld = false,           // 標記此工具不會與外部開放世界互動
    ReadOnly = true              // 標記為唯讀工具，呼叫時不會修改其所在環境
)]
public string EchoCustomized(string text)
{
    return text;
}
```

各屬性說明：

- **Name**：工具在 MCP 協定中的名稱。不指定時使用方法的 snake_case 命名。
- **Title**：人類可讀的工具標題，AI 看不到，可用於 UI 展示。
- **Description**：工具描述，會覆蓋方法 XML 註解中的描述。
- **Idempotent**：標記工具是否冪等。冪等工具在網路例外時可由用戶端安全重試。
- **OpenWorld**：標記工具是否會與外部開放世界互動（如存取網路 API）。
- **ReadOnly**：標記工具是否唯讀。唯讀工具在呼叫時不會修改其所在環境。
- **Structured**：控制是否為此工具產生結構化輸出（outputSchema + structuredContent）：
  - **未設定**：對於非空物件型別自動產生結構化輸出；對於可空物件或物件集合型別產生編譯錯誤 DM0102，要求顯式設定為 `false`
  - **true**：顯式啟用結構化輸出。僅對非空物件型別有效；對不可結構化的型別產生編譯錯誤 DM0101，對可空物件和物件集合產生編譯錯誤 DM0103
  - **false**：顯式禁用結構化輸出。對所有型別有效，不會產生 outputSchema

例如，傳回自訂物件型別的工具預設產生結構化輸出；若不需要，可顯式禁用：

```csharp
[McpServerTool(ReadOnly = true, Structured = false)]
public LocalTimeInfo GetTime() { ... }
```

### 參數與內容

#### 參數型別

工具方法支援多種參數型別。以下表格彙總了各類參數的標注方式、是否進入輸入 Schema、以及執行時值的來源。

> 表中「—」表示不出現在輸入 Schema 中。`[ToolParameter]` 還支援 `Name`（覆蓋 JSON 屬性名）和 `Description`（覆蓋參數描述），各型別通用，表中不單獨列出。

| 參數型別 | 標注方式 | 輸入 Schema | 執行時取值 |
|---|---|---|---|
| `IMcpServerCallToolContext` | 自動 | — | 轉發目前 `context` |
| JSON 可序列化型別（基本型別、string、物件等） | 自動 | 是 | `jsonArguments["name"]` 還原序列化為 .NET 型別 |
| `JsonElement` / `object` | 自動 | 是（任意 JSON） | `jsonArguments["name"]` 原樣傳遞 |
| 整個輸入物件 `[ToolParameter(Type = InputObject)]` | 必須標注 | 是（展開為屬性） | 整個 `jsonArguments` 還原序列化 |
| DI 注入 `[ToolParameter(Type = Injected)]`（可空） | 必須標注 | — | `GetService()`；未註冊→`null` |
| DI 注入 `[ToolParameter(Type = Injected)]`（非空） | 必須標注 | — | `GetService()`；未註冊→`McpToolServiceNotFoundException` |
| `CancellationToken` | 自動 | — | `context.CancellationToken` |

標注方式為自動的，由原始碼產生器根據參數型別自動推斷。任何參數標注為 `InputObject` 後**不允許**再有任何普通 JSON 參數。標注為 `Injected` 的參數值由 `IServiceProvider` 提供，需在伺服器初始化時設定 `WithServices()`，詳見[相依性注入](DependencyInjection.md)。

> **💡 提示**：`CancellationToken` 和 `IMcpServerCallToolContext` 推薦放在參數列表末尾，避免影響 JSON 參數的可讀性。

> **💡 提示**：對於 JSON 可序列化型別，可透過型別鑑別器支援多型；支援任意層級的屬性使用多型。詳見[多型型別](Polymorphism.md)。

#### IMcpServerCallToolContext 內容

`IMcpServerCallToolContext` 提供工具方法執行時的內容資訊，包括：

- 目前工具名稱（`context.Name`）
- 原始 JSON 輸入參數（`context.InputJsonArguments`）
- 要求中的 `_meta` 中繼資料（`context.Meta`），可用於分散式追蹤等情節
- MCP 伺服器資訊（`context.McpServer.ServerName`）
- 傳輸層工作階段（`context.TransportSession`），所有傳輸層均可用，包含：
  - `SessionId`：工作階段 ID，可用於區分不同用戶端連線（stdio 傳輸層下為 `null`）
  - `ConnectedClientInfo`：用戶端在初始化握手時提供的資訊（名稱、版本等）
  - `ConnectedClientCapabilities`：用戶端宣告的能力
  - `NegotiatedProtocolVersion`：協商出的協定版本
- HTTP 傳輸層內容（`context.HttpTransportContext`），僅在 HTTP 傳輸層下可用，包含：
  - `SessionId`：與 `TransportSession.SessionId` 相同
  - `Headers`：目前 HTTP 要求的要求標頭

> **💡 提示**：如果只需要區分不同用戶端，推薦使用 `context.TransportSession`，它在所有傳輸層（HTTP、stdio、InProcess、IPC）下均可用。`context.HttpTransportContext` 僅在 HTTP 傳輸層下可用，適合需要讀取 HTTP 要求標頭等情節。

> **⚠ 重要**：`IMcpServerCallToolContext` 執行個體**僅在目前工具方法執行期間有效**。不要將其儲存到靜態欄位或跨非同步邊界傳遞，因為工具呼叫結束後內容即失效。

#### 完整參數範例

以下範例展示了 `IMcpServerCallToolContext`、必需參數（無預設值）、可選參數（帶預設值）綜合用法：

```csharp
/// <summary>
/// 用於給 AI 偵錯使用的工具，原樣傳回一些資訊
/// </summary>
/// <param name="text">要原樣傳回的字串</param>
/// <param name="options">如何傳回字串</param>
/// <param name="count">要傳回的字串次數</param>
/// <param name="extraData">無意義的額外資訊</param>
[McpServerTool(Name = "echo_tool")]
public Task<EchoResult> EchoAsync(
    IMcpServerCallToolContext context,
    string text,
    EchoOptions options = EchoOptions.JsonObject,
    int count = 1,
    EchoExtraData? extraData = null)
{
    var session = context.TransportSession;
    var info = $"""
        Server name: {context.McpServer.ServerName}
        SessionId: {session?.SessionId}
        Client: {session?.ConnectedClientInfo?.Name} {session?.ConnectedClientInfo?.Version}
        Protocol: {session?.NegotiatedProtocolVersion}
        Headers: {string.Join(", ", context.HttpTransportContext?.Headers)}
        InputJsonArguments: {context.InputJsonArguments}
        """;
    var result = $"""
        Echoing text: {text}
        Options: {options}
        Count: {count}
        ExtraData: {extraData}
        """;
    return Task.FromResult(new EchoResult { Info = info, Result = result });
}
```

（範例中 `EchoOptions`、`EchoExtraData`、`EchoResult` 的定義見下方 [輔助型別](#輔助型別)。此範例傳回 `Task<EchoResult>`，`EchoResult` 為非空物件，預設啟用結構化輸出；傳回值行為的完整規則見下方[傳回值型別](#傳回值型別)表格。）

### 傳回值型別

方法的傳回值型別決定了編譯期原始碼產生器如何處理傳回值，以及執行時產生的 `CallToolResult` 結構。你可以透過 `Structured` 屬性（參見上文）進一步控制是否產生 MCP 結構化輸出。

以下表格彙總了所有支援的傳回值型別及其行為。推薦等級含義：

- **推薦**：傳回值行為完全符合 MCP 協定要求，無需任何轉換
- **支援**：本程式庫會對傳回值進行加工，使其符合 MCP 協定要求
- **不推薦**：部分情節下可能傳回不符合 MCP 協定的結果，可能導致某些 MCP 用戶端例外
- **自行處理**：本程式庫不干預傳回值，由開發者完全控制協定層資料

> 表中「—」表示 Structured 不適用，設 `true`→DM0101。`只可 false` 表示必須顯式設定，未設→DM0102，設 `true`→DM0103。

| 傳回值型別 | 推薦等級 | Structured 設定 | 輸出 Schema | 執行時行為 |
|---|---|---|---|---|
| `string` | 推薦 | — | — | TextContentBlock(text) |
| 非空自訂物件（record/class）`Foo` | 推薦 | 預設 true；可設 false 禁用 | 預設產生 OutputSchema | 預設：StructuredContent + TextContentBlock(json)；Structured=false 時僅 TextContentBlock(json) |
| `string?` | 支援 | — | — | TextContentBlock(text 或 "") |
| 可空基本型別 / 可空列舉（`int?`/`bool?`/`DayOfWeek?` 等） | 支援 | — | — | ToString()；null→"" |
| 基本型別 / 列舉（`int`/`bool`/`DayOfWeek` 等） | 支援 | — | — | ToString() |
| `JsonElement` | 支援 | — | — | TextContentBlock(json) |
| `void` / `Task` / `ValueTask` | 不推薦 | — | — | [] |
| 可空自訂物件（record/class）`Foo?` | 不推薦 | 只可 false | — | json→TextContentBlock；null→"" |
| 基本型別集合（`string[]`/`int[]`/`IReadOnlyList<DayOfWeek>` 等） | 不推薦 | — | — | 逐元素 ToString()→多個區塊；null/空→[] |
| 物件集合 `Foo[]` / `IReadOnlyList<Foo>` | 不推薦 | 只可 false | — | 逐元素 json→多個區塊；null/空→[] |
| `CallToolResult` | 自行處理 | — | — | 原樣傳回 |

方法可以是同步或非同步的：

- 同步：可使用上述所有種類的傳回值型別
- 非同步：可使用 `Task`、`Task<T>`、`ValueTask` 和 `ValueTask<T>` 的非同步傳回值。其中 `T` 即為表中對應的傳回值型別，行為一致

對於非空自訂物件，可透過型別鑑別器支援多型；支援任意層級的屬性使用多型。詳見[多型型別](Polymorphism.md)。

### 工具如何報告錯誤

工具可以透過兩種方式向用戶端報告錯誤，選擇哪種取決於你的情節：

#### 方式一：擲出 `McpToolUsageException`

適合「使用者用錯了這個工具」的情節，例如參數不合法。擲出後 MCP 協定層會自動向用戶端傳回 `isError: true`，無需改變傳回值型別：

```csharp
[McpServerTool]
public string Echo(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        throw new McpToolUsageException("text 參數不能為空白。");
    }
    return text;
}
```

#### 方式二：傳回 `CallToolResult.FromError()`

適合需要精確控制傳回內容或傳回結構化錯誤資訊的情節：

```csharp
[McpServerTool]
public CallToolResult SafeEcho(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return CallToolResult.FromError("text 參數不能為空白。");
    }
    return text; // string 可隱式轉換為 CallToolResult
}
```

兩種方式的區別：

| | `McpToolUsageException` | `CallToolResult.FromError()` |
|---|---|---|
| 傳回值型別 | 任意型別（不改變簽章） | 必須為 `CallToolResult` |
| 適用情節 | 快速失敗，不繼續執行 | 需要結構化錯誤資訊或精確控制傳回格式 |
| 優點 | 簡單直接，程式碼改動最小 | 彈性最高 |

### 輔助型別

以下是上文中複雜範例用到的輔助型別定義：

```csharp
/// <summary>
/// 如何傳回字串
/// </summary>
public enum EchoOptions
{
    /// <summary>
    /// 以純文字形式傳回
    /// </summary>
    PlainText,

    /// <summary>
    /// 以 JSON 物件形式傳回
    /// </summary>
    JsonObject,
}

/// <summary>
/// 無意義的額外資訊
/// </summary>
/// <param name="Data1">可供儲存的第 1 個值</param>
public record EchoExtraData(string Data1)
{
    /// <summary>
    /// 可供儲存的第 2 個值
    /// </summary>
    public string Data2 { get; init; } = "";
}

/// <summary>
/// 供 AI 偵錯使用的工具傳回值
/// </summary>
public record EchoResult
{
    /// <summary>
    /// 供 AI 偵錯使用的內容資訊
    /// </summary>
    public string Info { get; init; } = "";

    /// <summary>
    /// 供 AI 偵錯使用的結果資訊
    /// </summary>
    public string Result { get; init; } = "";
}
```

## 伺服器端進階初始化

### JSON 序列化與相依性注入

當你的工具參數或傳回值使用自訂型別時，需要傳入 JSON 序列化內容以支援 AOT 編譯。如果你希望工具類別支援相依性注入，則傳入 `IServiceProvider` 執行個體。MCP 程式庫的相依性注入由編譯期原始碼產生器實作，零反射。詳見[相依性注入](DependencyInjection.md)。

```csharp
var mcpServer = new McpServerBuilder("示例伺服器", "1.0.0")
    // 傳入 JSON 序列化內容（AOT 相容）
    .WithJsonSerializer(McpToolJsonContext.Default)

    // 傳入 IServiceProvider，支援工具類別的建構函式注入
    // 以及工具方法參數上的 [ToolParameter(Type = ToolParameterType.Injected)] 注入
    .WithServices(appServiceProvider)

    .WithTools(t => t
        // 普通註冊：每次呼叫建立新執行個體
        .WithTool(() => new SampleTools())
        // 相依性注入註冊：由 IServiceProvider 管理生命週期（必須已設定 WithServices）
        .WithTool<SampleTools2>()
    )

    .WithLocalHostHttp(5943, "mcp")
    .Build();
```

### 日誌整合

將 MCP 伺服器的內部日誌橋接到你自己的日誌系統，以便瞭解伺服器的工作健康狀況：

```csharp
var mcpServer = new McpServerBuilder("示例伺服器", "1.0.0")
    // 第二個參數控制傳輸層原始訊息的日誌詳細級別，預設不記錄
    .WithLogger(new McpLoggerBridge(myLogger), McpTransportRawMessageLoggingDetailLevel.Trimmed)
    // ... 其他設定
    .Build();
```

日誌橋接實作參考：

```csharp
internal class McpLoggerBridge(ILogger logger) : IMcpLogger
{
    public bool IsEnabled(LoggingLevel loggingLevel)
    {
        return logger.IsEnabled(loggingLevel.ToLogLevel());
    }

    public void Log<TState>(LoggingLevel loggingLevel, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        logger.Log(loggingLevel.ToLogLevel(), default, state, exception, formatter);
    }
}
```

### 要求攔截

透過繼承 `McpServerRequestHandlers` 並重寫方法，你可以攔截發往此 MCP 伺服器的所有要求，進行統一處理：

```csharp
var mcpServer = new McpServerBuilder("示例伺服器", "1.0.0")
    .WithRequestHandlers(s => new CustomRequestHandlers(s))
    // ... 其他設定
    .Build();
```

攔截器實作參考：

```csharp
internal class CustomRequestHandlers(McpServer server) : McpServerRequestHandlers(server)
{
    public override async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> rawRequest,
        string? toolName, IMcpServerTool? tool, IMcpServerCallToolContext? context)
    {
        var result = await base.CallToolAsync(rawRequest, toolName, tool, context);
        if (result.RawException is { } exception)
        {
            // 在工具呼叫例外時做額外的日誌記錄或警報
            Log.Error("工具呼叫例外", exception);
        }
        return result;
    }
}
```

### 傳輸層選擇

本程式庫支援多種傳輸層。選擇哪種取決於你的部署情節：

| 傳輸層 | 方法 | 適用情節 |
|--------|------|---------|
| Streamable HTTP（內建） | `.WithLocalHostHttp()` | 本機通訊，輕量零相依 |
| Streamable HTTP（TouchSocket） | `.WithTouchSocketHttp()` | 需要公網接聽，高效能 HTTP |
| stdio | `.WithStdio()` | 標準輸入輸出，MCP 官方推薦 |
| dotnetCampus.Ipc | `.WithDotNetCampusIpc()` | 本機高效能 IPC |

詳細說明和設定方法請參閱 [選擇傳輸層](Transport.md)。

## 用戶端呼叫工具

### 用戶端建構器多載

`McpClientBuilder` 的 `WithHttp` 和 `WithStdio` 方法各有兩個多載：簡單多載適合快速體驗，選項多載適合需要自訂設定的生產情節。

**HTTP 傳輸層：**

```csharp
// 簡單多載：僅指定 URL
var client = new McpClientBuilder("示例用戶端", "1.0.0")
    .WithHttp("http://localhost:3001/mcp")
    .Build();

// 選項多載：可設定自訂 HttpClient、逾時等
var client = new McpClientBuilder("示例用戶端", "1.0.0")
    .WithHttp(new HttpClientTransportOptions
    {
        ServerUrl = "http://localhost:3001/mcp",
        HttpClient = customHttpClient,  // 可注入帶驗證頭的 HttpClient
    })
    .Build();
```

**stdio 傳輸層：**

```csharp
// 簡單多載：指定命令和參數
var client = new McpClientBuilder("示例用戶端", "1.0.0")
    .WithStdio("npx", ["-y", "@modelcontextprotocol/server-everything", "stdio"])
    .Build();

// 選項多載：可設定環境變數
var client = new McpClientBuilder("示例用戶端", "1.0.0")
    .WithStdio(new StdioClientTransportOptions
    {
        Command = "python",
        Arguments = ["-m", "my_mcp_server"],
        EnvironmentVariables = new Dictionary<string, string>
        {
            ["PYTHONPATH"] = "/path/to/modules",
        },
    })
    .Build();
```

### 基礎呼叫

單一 MCP 用戶端的典型呼叫流程：

```csharp
var client = new McpClientBuilder("示例用戶端", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

// 可選：提前連線，統一擷取例外
await client.EnsureConnectedAsync();

// 列出工具
var tools = await client.ListToolsAsync();
foreach (var tool in tools.Tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

// 呼叫工具
var arguments = JsonSerializer.SerializeToElement(new { text = "Hello" });
var result = await client.CallToolAsync("echo_tool", arguments);
Console.WriteLine(result.Content);
```

### 多伺服器管理（智慧型代理人情節）

在智慧型代理人程式中，通常需要同時管理多個 MCP 伺服器（內建工具、外部服務、崗位程式等）。完整的 MCP 伺服器管理器範例請參閱 [McpServerManager](McpServerManager.md)。
