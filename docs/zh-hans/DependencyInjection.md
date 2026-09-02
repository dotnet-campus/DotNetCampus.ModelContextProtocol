# 依赖注入

MCP 库的依赖注入完全由**编译期源生成器**实现，运行时**零反射**。本文解释其工作原理、使用方式，以及与常规 DI 容器的关键区别。

## 核心原理

当你写下以下代码：

```csharp
var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
    .WithServices(appServiceProvider)
    .WithTools(tools => tools
        .WithTool<MyTool>()
        .WithTool<MyOtherTool>())
    .Build();
```

编译期实际执行的流程是：

1. **`WithServices(IServiceProvider)`** 仅保存你的 `IServiceProvider` 引用，**不做任何服务注册或容器扫描**。
2. **`WithTool<MyTool>()`** 的方法体本身是一个 `throw new InvalidOperationException()`（永远不会执行）。C# 12 **Interceptors** 特性在编译期拦截了这次调用。
3. **编译期生成的拦截器代码** 通过 Roslyn 分析 `MyTool` 的构造函数签名，为每个构造参数生成显式的 `serviceProvider.GetService(typeof(TParam))` 调用。

> **关键结论：工具类不需要注册到 DI 容器。** `IServiceProvider` 只用于解析工具构造函数的**参数类型**（如 `ILogger`、`HttpClient` 等）。

## 两种注入方式

### 方式一：构造函数注入（`WithTool<T>()`，推荐）

适合工具类有多个共享依赖的场景。源生成器（`WithToolInterceptorGenerator`）在编译期找到构造函数，为每个参数生成 `GetService` 调用：

```csharp
// 用户代码
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
    /// 处理输入，返回结果。
    /// </summary>
    [McpServerTool]
    public string DoSomething(string input)
    {
        _logger.Info($"processing: {input}");
        return _dataService.Process(input);
    }
}

// 注册 —— 无需传入工厂
builder.WithServices(appServiceProvider);
builder.WithTool<MyTool>();  // 拦截器自动生成依赖注入代码
```

编译期生成的等效代码（简化）：

```csharp
// 拦截器在编译期生成，运行时无反射
var factory = () => new MyTool(
    (ILogger?)serviceProvider.GetService(typeof(ILogger))
        ?? throw new InvalidOperationException("无法解析 ILogger。"),
    (IDataService?)serviceProvider.GetService(typeof(IDataService))
        ?? throw new InvalidOperationException("无法解析 IDataService。"));
```

### 方式二：参数注入（`[ToolParameter(Type = ToolParameterType.Injected)]`）

适合仅个别参数需要 DI 的场景。源生成器（`McpServerToolSourceBuilder`）为该参数生成独立的 `GetService` 调用：

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

编译期生成的等效代码：

```csharp
// 可空类型 → TryGetService，解析失败返回 null
var logger = context.TryGetService<ILogger>();

// 不可空类型 → EnsureGetService，解析失败抛出异常
var requiredService = context.EnsureGetService<IRequiredService>("IRequiredService");
```

## 需要配置什么

| 操作 | 是否需要 | 说明 |
|------|---------|------|
| 注册工具类型到 DI 容器 | **不需要** | 源生成器已完成构造函数分析，运行时直接用 `new` 构造 |
| 注册构造参数类型到 DI 容器 | **需要** | 如 `ILogger`、`IDataService` 等必须能从 `IServiceProvider` 解析 |
| 调用 `WithServices()` | **需要** | 将你的 `IServiceProvider` 传给 MCP 服务器 |
| 调用 `WithTool<T>()`（无工厂） | **需要** | 触发源生成器为此类型生成 DI 代码 |
| 调用 `WithTool(() => new MyTool(dep1))` | 可选 | 手动创建实例，此时不需要 `IServiceProvider` |

## 与常规 DI 容器的区别

| | 常规 DI 容器（如 `Microsoft.Extensions.DI`） | MCP 库 |
|---|---|---|
| 服务发现 | 运行时扫描程序集 | 编译期 Roslyn 分析源码 |
| 实例创建 | 运行时 `Activator.CreateInstance` | 编译期生成 `new T(...)` 表达式 |
| 工具注册 | `services.AddTransient<MyTool>()` | **不需要** |
| 参数注入 | 容器递归解析类型树 | 编译期生成 `serviceProvider.GetService(typeof(T))` |
| 解析失败 | 运行时抛异常 | 可空参数返回 `null`，不可空参数抛异常 |

## 安全性

如果 `WithTool<T>()` 的拦截器缺失（如忘记引用 Analyzer NuGet 包），真方法体是 `throw new InvalidOperationException`——启动时立即失败并给出明确报错，不会静默使用错误的解析方式。

## 为什么不用反射

1. **AOT 兼容**：不使用 `Activator.CreateInstance` 和程序集扫描，完全兼容 NativeAOT 编译。
2. **编译期错误检测**：构造函数参数无法解析时，编译期直接报 `#error`，不必等到运行时。
3. **零开销**：生成代码的性能与手写 `new MyTool(dep1, dep2)` 完全相同。
