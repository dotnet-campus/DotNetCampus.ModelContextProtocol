# MCP 工具设计

## 概述

DotNetCampus.ModelContextProtocol 的 MCP 工具机制基于 **源生成器** 实现，通过分析标记了 `[McpServerTool]` 特性的方法，自动生成桥接类（Bridge），将 MCP 协议层与用户代码连接起来。

### 核心组件

1. **`McpServerToolAttribute`** - 用户标记工具方法的特性
2. **`McpServerToolGenerator`** - 主源生成器，为每个工具方法生成桥接类
3. **`McpServerToolSourceBuilder`** - 无状态的源代码构建器，负责生成桥接类的具体代码
4. **`InterceptorGenerator`** - 拦截器生成器，拦截 `WithTool<T>()` 调用并自动发现类中的所有工具
5. **`IMcpServerTool`** - 工具桥接类的接口

### 工作流程

```
用户代码标记 [McpServerTool]
         ↓
McpServerToolGenerator 分析方法
         ↓
McpServerToolSourceBuilder 生成桥接类
         ↓
InterceptorGenerator 拦截 WithTool<T>() 调用
         ↓
为类中所有工具方法创建桥接实例
```

---

## 一、用户侧 API

### 1.1 `[McpServerTool]` 特性

用户通过在方法上标记 `[McpServerTool]` 特性来声明一个 MCP 工具：

```csharp
public class MyTools
{
    /// <summary>
    /// 计算两个数的和
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    public int Add(int a, int b) => a + b;
}
```

### 1.2 参数与返回值类型

详见 [快速使用文档](../quickstart/README.md#mcp-工具方法声明)，包括：

- 支持的参数类型（普通参数、InputObject、Injected、Context、CancellationToken、JsonElement）
- 支持的返回值类型（string、void、可序列化对象、CallToolResult、CallToolResult<T>）
- 同步与异步方法
- 类型多态支持

---

## 二、源生成器架构

### 2.1 `McpServerToolGenerator`

**职责**：为每个标记了 `[McpServerTool]` 的方法生成独立的桥接类。

**输入**：Roslyn 增量生成器管道，筛选出所有标记了 `McpServerToolAttribute` 的方法

**输出**：每个方法一个 `.cs` 文件，包含桥接类定义

### 2.2 `McpServerToolSourceBuilder`

**职责**：提供一组扩展方法，将 `McpServerToolGeneratingModel` 转换为具体的 C# 代码。

**设计原则**：
- **无状态**：所有方法都是静态扩展方法
- **链式调用**：利用 `SourceTextBuilder` 的流畅 API
- **职责单一**：每个方法只负责生成一种代码结构

**核心扩展方法**：

- `AddGetToolDefinitionMethod` - 生成 `GetToolDefinition` 方法，返回工具定义信息
- `AddGetInputSchemaMethod` - 生成 `GetInputSchema` 方法，返回参数的 JSON Schema
- `AddGetOutputSchemaMethod` - 生成 `GetOutputSchema` 方法（可选，仅当有结构化返回值时）
- `AddCallToolMethod` - 生成 `CallTool` 方法，处理参数反序列化、方法调用和返回值序列化

### 2.3 `InterceptorGenerator`

**职责**：拦截用户代码中的 `WithTool<T>()` 调用，自动发现并为类型 `T` 中所有标记了 `[McpServerTool]` 的方法创建桥接实例。

**工作原理**：
1. 扫描所有 `McpServerToolsBuilder.WithTool<T>(...)` 调用
2. 分析泛型参数 `T`，找出其中所有 `[McpServerTool]` 方法
3. 为每个类型生成一个拦截方法
4. 在拦截方法中创建所有工具桥接类实例

---

## 三、生成的桥接类结构

源生成器为每个工具方法生成一个桥接类，实现 `IMcpServerTool` 接口。以下是一个真实的生成代码示例：

```csharp
#nullable enable
using global::System.Text.Json;
using global::DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.SampleMcpServer.McpTools;

/// <summary>
/// 为 <see cref="global::DotNetCampus.SampleMcpServer.McpTools.PolymorphicTool.TestPolymorphicParameter"/> 方法生成的 MCP 服务器工具桥接类。
/// </summary>
public sealed class PolymorphicTool_TestPolymorphicParameter_Bridge(global::System.Func<global::DotNetCampus.SampleMcpServer.McpTools.PolymorphicTool> targetFactory) : global::DotNetCampus.ModelContextProtocol.Servers.IMcpServerTool
{
    private readonly global::System.Func<global::DotNetCampus.SampleMcpServer.McpTools.PolymorphicTool> _targetFactory = targetFactory;

    private global::DotNetCampus.SampleMcpServer.McpTools.PolymorphicTool Target => _targetFactory();

    /// <inheritdoc />
    public string ToolName { get; } = "test_polymorphic_parameter";

    /// <inheritdoc />
    public global::DotNetCampus.ModelContextProtocol.Protocol.Messages.Tool GetToolDefinition(global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledSchemaJsonContext jsonContext) => new()
    {
        Name = "test_polymorphic_parameter",
        Description = "测试多态参数的工具方法",
        InputSchema = global::System.Text.Json.JsonSerializer.SerializeToElement(GetInputSchema(jsonContext), jsonContext.CompiledJsonSchema),
        Annotations = new global::DotNetCampus.ModelContextProtocol.Protocol.Messages.ToolAnnotations
        {
            ReadOnlyHint = true,
        },
    };

    private global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema GetInputSchema(global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledSchemaJsonContext jsonContext) => new global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema
    {
        Type = global::System.Text.Json.JsonSerializer.SerializeToElement("object", jsonContext.String),
        Required = [ "param" ],
        Properties = new global::System.Collections.Generic.Dictionary<string, global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema>
        {
            [ "param" ] = new global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema
            {
                Type = global::System.Text.Json.JsonSerializer.SerializeToElement("object", jsonContext.String),
                Description = "多态参数",
                Required = [ "type" ],
                AnyOf = 
                [
                    new global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema
                    {
                        Properties = new global::System.Collections.Generic.Dictionary<string, global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema>
                        {
                            [ "type" ] = new global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema
                            {
                                Const = "a",
                            },
                            [ "foo" ] = new global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema
                            {
                                Type = global::System.Text.Json.JsonSerializer.SerializeToElement(new[] { "string", "null" }, jsonContext.StringArray),
                            },
                        },
                    },
                    new global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema
                    {
                        Properties = new global::System.Collections.Generic.Dictionary<string, global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema>
                        {
                            [ "type" ] = new global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema
                            {
                                Const = "b",
                            },
                            [ "bar" ] = new global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema
                            {
                                Type = global::System.Text.Json.JsonSerializer.SerializeToElement(new[] { "integer", "null" }, jsonContext.StringArray),
                            },
                        },
                    },
                    new global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema
                    {
                        Properties = new global::System.Collections.Generic.Dictionary<string, global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema>
                        {
                            [ "type" ] = new global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema
                            {
                                Const = "c",
                            },
                            [ "baz" ] = new global::DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema
                            {
                            },
                        },
                    },
                ],
            },
        },
    };

    /// <inheritdoc />
    public global::System.Threading.Tasks.ValueTask<global::DotNetCampus.ModelContextProtocol.Protocol.Messages.CallToolResult> CallTool(global::DotNetCampus.ModelContextProtocol.Servers.IMcpServerCallToolContext context)
    {
        global::System.Text.Json.JsonElement jsonArguments = context.InputJsonArguments;
        global::System.Text.Json.Serialization.JsonSerializerContext jsonSerializerContext = context.JsonSerializerContext;
        global::System.Threading.CancellationToken cancellationToken = context.CancellationToken;
        var param = jsonArguments.TryGetProperty("param", out var paramProperty)
            ? context.EnsureDeserialize<global::DotNetCampus.SampleMcpServer.McpTools.PolymorphicBase>(paramProperty, "PolymorphicBase", "DotNetCampus.SampleMcpServer.McpTools.PolymorphicBase", "type", ["a", "b", "c"])
            : throw new global::DotNetCampus.ModelContextProtocol.Exceptions.McpToolMissingRequiredArgumentException("param");
        var result = Target.TestPolymorphicParameter(param!);
        return global::System.Threading.Tasks.ValueTask.FromResult((global::DotNetCampus.ModelContextProtocol.Protocol.Messages.CallToolResult.FromResult(result)).Structure(jsonSerializerContext));
    }
}
```

---

## 四、设计原则

### 4.1 零运行时分配

- 使用 `JsonSerializer.SerializeToElement` 直接序列化到 `JsonElement`
- 避免中间字符串分配
- 流式处理 JSON 数据

### 4.2 编译时类型安全

- 所有类型信息在源生成时确定
- 避免运行时反射
- 利用 Roslyn 的符号分析

### 4.3 错误友好

- 生成的代码包含详细的类型信息注释
- 参数验证时提供清晰的错误消息
- 支持 nullable 引用类型

---

## 五、与 MCP 协议的映射

### 5.1 `tools/list` 请求

*(此处将填入真实调试时获得的协议数据)*

### 5.2 `tools/call` 请求

*(此处将填入真实调试时获得的协议数据)*

---

## 六、核心文件清单

| 文件路径                                                                                                 | 职责                     |
| -------------------------------------------------------------------------------------------------------- | ------------------------ |
| `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/McpServerToolGenerator.cs`                    | 主源生成器               |
| `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerToolSourceBuilder.cs` | 源代码构建器（核心逻辑） |
| `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/InterceptorGenerator.cs`                      | 拦截器生成器             |
| `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/McpServerToolGeneratingModel.cs`       | 工具方法模型             |
| `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/JsonPropertySchemaInfo.cs`             | JSON Schema 生成辅助     |
| `src/DotNetCampus.ModelContextProtocol/CompilerServices/McpServerToolAttribute.cs`                       | 用户标记特性             |
| `src/DotNetCampus.ModelContextProtocol/Servers/IMcpServerTool.cs`                                        | 桥接类接口               |
