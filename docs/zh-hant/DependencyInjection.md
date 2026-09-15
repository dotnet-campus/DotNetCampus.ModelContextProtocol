# 相依性注入

MCP 程式庫的相依性注入完全由**編譯期原始碼產生器**實作，執行階段**零反射**。本文解釋其運作原理、使用方式，以及與一般 DI 容器的關鍵差異。

## 核心原理

當你寫下以下程式碼：

```csharp
var mcpServer = new McpServerBuilder("示例伺服器", "1.0.0")
    .WithServices(appServiceProvider)
    .WithTools(tools => tools
        .WithTool<MyTool>()
        .WithTool<MyOtherTool>())
    .Build();
```

編譯期實際執行的流程是：

1. **`WithServices(IServiceProvider)`** 僅儲存你的 `IServiceProvider` 參考，**不做任何服務註冊或容器掃描**。
2. **`WithTool<MyTool>()`** 的方法體本身就是一個 `throw new InvalidOperationException()`（永遠不會執行）。C# 12 **Interceptors** 特性在編譯期攔截了這次呼叫。
3. **編譯期產生的攔截器程式碼** 透過 Roslyn 分析 `MyTool` 的建構函式簽章，為每個建構參數產生明確的 `serviceProvider.GetService(typeof(TParam))` 呼叫。

> **關鍵結論：工具類別不需要註冊到 DI 容器。** `IServiceProvider` 只用於解析工具建構函式的**參數型別**（如 `ILogger`、`HttpClient` 等）。

## 兩種注入方式

### 方式一：建構函式注入（`WithTool<T>()`，推薦）

適合工具類別有多個共用相依性的場景。原始碼產生器（`WithToolInterceptorGenerator`）在編譯期找到建構函式，為每個參數產生 `GetService` 呼叫：

```csharp
// 使用者程式碼
public class MyTool
{
    private readonly ILogger _logger;
    private readonly IDataService _dataService;

    public MyTool(ILogger logger, IDataService dataService)
    {
        _logger = logger;
        _dataService = dataService;
    }

    /// <summary>
    /// 處理輸入，傳回結果。
    /// </summary>
    [McpServerTool]
    public string DoSomething(string input)
    {
        _logger.Info($"processing: {input}");
        return _dataService.Process(input);
    }
}

// 註冊 —— 無需傳入工廠
builder.WithServices(appServiceProvider);
builder.WithTool<MyTool>();  // 攔截器自動產生相依性注入程式碼
```

編譯期產生的等效程式碼（簡化）：

```csharp
// 攔截器在編譯期產生，執行階段無反射
var factory = () => new MyTool(
    (ILogger?)serviceProvider.GetService(typeof(ILogger))
        ?? throw new InvalidOperationException("無法解析 ILogger。"),
    (IDataService?)serviceProvider.GetService(typeof(IDataService))
        ?? throw new InvalidOperationException("無法解析 IDataService。"));
```

### 方式二：參數注入（`[ToolParameter(Type = ToolParameterType.Injected)]`）

適合僅個別參數需要 DI 的場景。原始碼產生器（`McpServerToolSourceBuilder`）為該參數產生獨立的 `GetService` 呼叫：

```csharp
public class SampleTools
{
    [McpServerTool]
    public string FormatMessage(
        string text,
        [ToolParameter(Type = ToolParameterType.Injected)] ILogger logger)
    {
        logger.Info($"formatting: {text}");
        return text.ToUpper();
    }
}
```

編譯期產生的等效程式碼：

```csharp
// 可空型別 → TryGetService，解析失敗傳回 null
var logger = context.TryGetService<ILogger>();

// 不可空型別 → EnsureGetService，解析失敗擲回例外狀況
var requiredService = context.EnsureGetService<IRequiredService>("IRequiredService");
```

## 需要組態什麼

| 操作 | 是否需要 | 說明 |
|------|---------|------|
| 註冊工具型別到 DI 容器 | **不需要** | 原始碼產生器已完成建構函式分析，執行階段直接用 `new` 建構 |
| 註冊建構參數型別到 DI 容器 | **需要** | 如 `ILogger`、`IDataService` 等必須能從 `IServiceProvider` 解析 |
| 呼叫 `WithServices()` | **需要** | 將你的 `IServiceProvider` 傳給 MCP 伺服器 |
| 呼叫 `WithTool<T>()`（無工廠） | **需要** | 觸發原始碼產生器為此型別產生 DI 程式碼 |
| 呼叫 `WithTool(() => new MyTool(dep1))` | 可選 | 手動建立執行個體，此時不需要 `IServiceProvider` |

## 與一般 DI 容器的差異

| | 一般 DI 容器（如 `Microsoft.Extensions.DI`） | MCP 程式庫 |
|---|---|---|
| 服務探索 | 執行階段掃描組件 | 編譯期 Roslyn 分析原始碼 |
| 執行個體建立 | 執行階段 `Activator.CreateInstance` | 編譯期產生 `new T(...)` 陳述式 |
| 工具註冊 | `services.AddTransient<MyTool>()` | **不需要** |
| 參數注入 | 容器遞迴解析型別樹 | 編譯期產生 `serviceProvider.GetService(typeof(T))` |
| 解析失敗 | 執行階段擲回例外狀況 | 可空參數傳回 `null`，不可空參數擲回例外狀況 |

## 安全性

如果 `WithTool<T>()` 的攔截器缺失（如忘記參考 Analyzer NuGet 套件），真方法體是 `throw new InvalidOperationException`——啟動時立即失敗並給出明確報錯，不會靜默使用錯誤的解析方式。

## 為什麼不用反射

1. **AOT 相容**：不使用 `Activator.CreateInstance` 和組件掃描，完全相容 NativeAOT 編譯。
2. **編譯期錯誤偵測**：建構函式參數無法解析時，編譯期直接報 `#error`，不必等到執行階段。
3. **零開銷**：產生程式碼的效能與手寫 `new MyTool(dep1, dep2)` 完全相同。
