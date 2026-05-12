# Tools

Tools 用於讓用戶端請求伺服器端執行動作。我們假設你在閱讀本文前，已經完成了 [快速開始](QuickStart.md) 中 MCP 伺服器和用戶端的建置。

## 伺服器端實作工具

### 簡單範例

一個簡單的 MCP 工具實作如下：

```csharp
public class SampleTools
{
    /// <summary>
    /// 用于给 AI 调试使用的工具，原样返回一些信息
    /// </summary>
    /// <param name="text">要原样返回的字符串</param>
    /// <returns>原样返回的字符串</returns>
    [McpServerTool(ReadOnly = true)]
    public string EchoTool(string text)
    {
        return text;
    }
}
```

在這個範例中：

- 註解會成為此工具描述的重要部分，並在 MCP 協定中傳送給用戶端，所以寫好註解對大語言模型正確使用工具非常重要；另外，不用太在意註解的多語言問題，因為大語言模型不在乎你用什麼語言描述工具
- 如果參數是複雜的資料型別或列舉，不必詳細在參數註解中詳細描述每個內部屬性或欄位，因為本程式庫會自動遞迴地擷取註解，並將它們包含在 MCP 協定中傳送給用戶端
- 工具的名稱預設使用 snake_case 命名法轉換方法名，本例中，你會得到 `echo_tool`

### 自訂工具屬性

`[McpServerTool]` 特性支援多個屬性，讓你精細控制工具在 MCP 協定中的行為和中繼資料：

```csharp
/// <summary>
/// 用于给 AI 调试使用的工具，原样返回一些信息
/// </summary>
/// <param name="text">要原样返回的字符串</param>
[McpServerTool(
    Name = "echo_tool",          // 指定工具名称，避免方法名与工具名强绑定（例如避免 Async 后缀影响工具名）
    Title = "原样输出",          // 给人类阅读的工具名称，AI 看不到，可用于 UI 展示
    Description = "用于给 AI 调试使用的工具，原样返回一些信息",  // 覆盖方法注释中的描述
    Idempotent = true,           // 标记为幂等工具，客户端可以安全地重试调用
    OpenWorld = false,           // 标记此工具不会与外部开放世界交互
    ReadOnly = true              // 标记为只读工具，调用时不会修改其环境
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

### 參數與內容

#### 隱含參數型別

工具方法可以接收以下隱含參數，按需加入在方法簽章中即可：

- 任意可被 JSON 還原序列化的型別（基本型別、陣列、物件等）—— 由 MCP 用戶端傳入
- `CancellationToken` —— 當用戶端取消工具呼叫時觸發，建議總是作為最後一個參數宣告
- `IMcpServerCallToolContext` —— 提供目前工具呼叫的內容資訊
- `JsonElement` —— 接收任意 JSON 資料，適合參數結構不確定的情節

#### 明確參數型別（`[ToolParameter]` 特性）

透過 `[ToolParameter]` 特性標記特殊參數行為：

- `[ToolParameter(Type = ToolParameterType.InputObject)]`：此參數負責接收整個工具呼叫的輸入物件（還原序列化到一個型別）。使用此標記後**不允許**再有其他一般參數。
- `[ToolParameter(Type = ToolParameterType.Injected)]`：此參數由相依性注入架構自動注入，不由 MCP 協定層傳入。需要已在伺服器初始化時組態 `IServiceProvider`。詳見[相依性注入](DependencyInjection.md)。

#### IMcpServerCallToolContext 內容

`IMcpServerCallToolContext` 提供工具方法執行時的內容資訊，包括：

- 目前工具名稱（`context.Name`）
- 原始 JSON 輸入參數（`context.InputJsonArguments`）
- 要求中的 `_meta` 中繼資料（`context.Meta`），可用於分散式追蹤等情節
- MCP 伺服器資訊（`context.McpServer.ServerName`）
- HTTP 傳輸層內容（`context.HttpTransportContext`，含 SessionId、Headers 等）

> **⚠ 重要**：`IMcpServerCallToolContext` 執行個體**僅在目前工具方法執行期間有效**。不要將其儲存到靜態欄位或跨非同步邊界傳遞，因為工具呼叫結束後內容即失效。

#### 完整參數範例

以下範例展示了 `IMcpServerCallToolContext`、帶預設值的參數、可為 Null 的參數的綜合用法：

```csharp
/// <summary>
/// 用于给 AI 调试使用的工具，原样返回一些信息
/// </summary>
/// <param name="text">要原样返回的字符串</param>
/// <param name="options">如何返回字符串</param>
/// <param name="count">要返回的字符串次数</param>
/// <param name="extraData">无意义的额外信息</param>
[McpServerTool(Name = "echo_tool")]
public Task<EchoResult> EchoAsync(
    IMcpServerCallToolContext context,
    string text,
    EchoOptions options = EchoOptions.JsonObject,
    int count = 1,
    EchoExtraData? extraData = null)
{
    var info = $"""
        Server name: {context.McpServer.ServerName}
        SessionId: {context.HttpTransportContext?.SessionId}
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

（範例中 `EchoOptions`、`EchoExtraData`、`EchoResult` 的定義見下方 [輔助型別](#輔助型別)。）

### 回傳值型別

方法的回傳值可以是以下型別：

- `string`：傳回給 AI 的字串（通常是可被 AI 理解的自然語言）
- `void`：沒有回傳值。**請注意**，雖然這是 MCP 協定支援的型別，但有些 MCP 用戶端會在伺服器傳回空白結果時出現例外；此時建議改為 `string` 回傳值，傳回空字串
- 任意可被 JSON 序列化的型別（根據 MCP 協定規範，**回傳值只能是物件型別**，不能是陣列或原始型別）
- `CallToolResult`：通用的工具呼叫結果，即 MCP 協定層的最終資料結構。使用此回傳值型別，你可以直接在協定層控制傳回給 AI 的資料
- `CallToolResult<T>`：帶有結構化資料型別的工具呼叫結果，透過 `CallToolResult<T>.FromResult(result)` 方法建立執行個體。`T` 是任意可被 JSON 序列化的型別。使用此回傳值型別，你在保持結構化回傳值功能的同時，仍然具備協定層控制傳回資料的能力

**特別的**，當回傳值是可被 JSON 序列化的物件時，按 MCP 協定規範，我們會傳回結構化資料，並在一般字串回傳值中也包含此資料的 JSON 序列化字串（以供相容）。同時此工具還會被標記為「具有結構化回傳值」。

**特別的**，MCP 協定規範要求集合型別不允許作為回傳值。本庫會檢查 MCP 工具是否存在集合回傳值，如果存在，會報告 DM0101 錯誤。

### 同步與非同步

方法可以是同步或非同步的：

- 同步：支援上述所有種類的回傳值型別
- 非同步：支援 `Task`、`Task<T>`、`ValueTask` 和 `ValueTask<T>` 的非同步回傳值

### 工具如何報告錯誤

工具可以透過兩種方式向用戶端報告錯誤，選擇哪種取決於你的情節：

#### 方式一：擲出 `McpToolUsageException`

適合「使用者用錯了這個工具」的情節，例如參數不合法。擲出後 MCP 協定層會自動向用戶端傳回 `isError: true`，無需改變回傳值型別：

```csharp
[McpServerTool]
public string Echo(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        throw new McpToolUsageException("text 参数不能为空。");
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
        return CallToolResult.FromError("text 参数不能为空。");
    }
    return text;
}
```

兩種方式的區別：

| | `McpToolUsageException` | `CallToolResult.FromError()` |
|---|---|---|
| 回傳值型別 | 任意型別（不改變簽章） | 必須為 `CallToolResult` |
| 適用情節 | 快速失敗，不繼續執行 | 需要結構化錯誤資訊或精確控制傳回格式 |
| 優點 | 簡單直接，程式碼改動最小 | 彈性最高 |

### 輔助型別

以下是上文中複雜範例用到的輔助型別定義：

```csharp
/// <summary>
/// 如何返回字符串
/// </summary>
public enum EchoOptions
{
    /// <summary>
    /// 以纯文本形式返回
    /// </summary>
    PlainText,

    /// <summary>
    /// 以 JSON 对象形式返回
    /// </summary>
    JsonObject,
}

/// <summary>
/// 无意义的额外信息
/// </summary>
/// <param name="Data1">可供保存的第 1 个值</param>
public record EchoExtraData(string Data1)
{
    /// <summary>
    /// 可供保存的第 2 个值
    /// </summary>
    public string Data2 { get; init; } = "";
}

/// <summary>
/// 供 AI 调试使用的工具返回值
/// </summary>
public record EchoResult
{
    /// <summary>
    /// 供 AI 调试使用的上下文信息
    /// </summary>
    public string Info { get; init; } = "";

    /// <summary>
    /// 供 AI 调试使用的结果信息
    /// </summary>
    public string Result { get; init; } = "";
}
```

## 伺服器端進階初始化

### JSON 序列化與相依性注入

當你的工具參數或回傳值使用自訂型別時，需要傳入 JSON 序列化內容以支援 AOT 編譯。如果你希望工具類別支援相依性注入，則傳入 `IServiceProvider` 執行個體。MCP 程式庫的相依性注入由編譯期原始碼產生器實作，零反射。詳見[相依性注入](DependencyInjection.md)。

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    // 传入 JSON 序列化上下文（AOT 兼容）
    .WithJsonSerializer(McpToolJsonContext.Default)

    // 传入 IServiceProvider，支持工具类的构造函数注入
    // 以及工具方法参数上的 [ToolParameter(Type = ToolParameterType.Injected)] 注入
    .WithServices(appServiceProvider)

    .WithTools(t => t
        // 普通注册：每次调用创建新实例
        .WithTool(() => new SampleTools())
        // 依赖注入注册：由 IServiceProvider 管理生命周期（必须已配置 WithServices）
        .WithTool<SampleTools2>()
    )

    .WithLocalHostHttp(5943, "mcp")
    .Build();
```

### 日誌整合

將 MCP 伺服器的內部日誌橋接到你自己的日誌系統，以便了解伺服器的工作健康狀況：

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    // 第二个参数控制传输层原始消息的日志详细级别，默认不记录
    .WithLogger(new McpLoggerBridge(myLogger), McpTransportRawMessageLoggingDetailLevel.Trimmed)
    // ... 其他配置
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
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    .WithRequestHandlers(s => new CustomRequestHandlers(s))
    // ... 其他配置
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
            // 在工具调用异常时做额外的日志记录或告警
            Log.Error("工具调用异常", exception);
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
| stdio | `.WithStdio()` | 標準輸入輸出，MCP 官方建議 |
| dotnetCampus.Ipc | `.WithDotNetCampusIpc()` | 本機高效能 IPC |

詳細說明和組態方法請參閱 [選擇傳輸層](Transport.md)。

## 用戶端呼叫工具

### 用戶端建構器多載

`McpClientBuilder` 的 `WithHttp` 和 `WithStdio` 方法各有兩個多載：簡單多載適合快速體驗，選項多載適合需要自訂組態的生產情節。

**HTTP 傳輸層：**

```csharp
// 简单重载：仅指定 URL
var client = new McpClientBuilder("示例客户端", "1.0.0")
    .WithHttp("http://localhost:3001/mcp")
    .Build();

// 选项重载：可配置自定义 HttpClient、超时等
var client = new McpClientBuilder("示例客户端", "1.0.0")
    .WithHttp(new HttpClientTransportOptions
    {
        ServerUrl = "http://localhost:3001/mcp",
        HttpClient = customHttpClient,  // 可注入带认证头的 HttpClient
    })
    .Build();
```

**stdio 傳輸層：**

```csharp
// 简单重载：指定命令和参数
var client = new McpClientBuilder("示例客户端", "1.0.0")
    .WithStdio("npx", ["-y", "@modelcontextprotocol/server-everything", "stdio"])
    .Build();

// 选项重载：可配置环境变量
var client = new McpClientBuilder("示例客户端", "1.0.0")
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
var client = new McpClientBuilder("示例客户端", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

// 可选：提前连接，统一捕获异常
await client.EnsureConnectedAsync();

// 列出工具
var tools = await client.ListToolsAsync();
foreach (var tool in tools.Tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

// 调用工具
var arguments = JsonSerializer.SerializeToElement(new { text = "Hello" });
var result = await client.CallToolAsync("echo_tool", arguments);
Console.WriteLine(result.Content);
```

### 多伺服器管理（智慧型代理人情節）

在智慧型代理人程式中，通常需要同時管理多個 MCP 伺服器（內建工具、外部服務、崗位程式等）。完整的 MCP 伺服器管理器範例請參閱 [McpServerManager](McpServerManager.md)。
