# 快速使用

## MCP 工具方法声明

```csharp
public class SampleTools
{
    /// <summary>
    /// 用于给 AI 调试使用的工具，原样返回一些信息
    /// </summary>
    /// <param name="text">要原样返回的字符串</param>
    /// <returns>原样返回的字符串</returns>
    [McpServerTool(ReadOnly = true)]
    public string Echo(string text)
    {
        return text;
    }
}
```

### 支持的类型

方法参数可以是任意数量的，支持以下类型：

- 隐式类型：
    - 任意可被 JSON 反序列化的类型
    - `CancellationToken`: 表示取消令牌
    - `IMcpServerCallToolContext`: 表示当前工具方法的上下文信息
    - `JsonElement`：表示任意 JSON 数据 **请注意，虽然这是 MCP 协议支持的类型，但有些 MCP 客户端（如 Copilot 智能体）不接受这种类型作为输入参数；当存在时，会导致整个 MCP 服务器不可用**
- 显式类型：
    - `[ToolParameter(Type = ToolParameterType.InputObject)]`：表示此参数负责接收整个工具调用的输入对象，此时不允许再有其他普通参数
    - `[ToolParameter(Type = ToolParameterType.Injected)]`：表示此参数由依赖注入框架自动注入，不由 MCP 协议层传入

方法的返回值可以是以下类型：

- `string`: 表示返回给 AI 的字符串（通常是可被 AI 理解的自然语言）
- `void`: 表示没有返回值 **请注意，虽然这是 MCP 协议支持的类型，但有些 MCP 客户端会在 MCP 服务器返回空结果时出现异常；此时建议改为 `string` 返回值，并返回空字符串**
- 任意可被 JSON 序列化的类型
- `CallToolResult`: 通用的工具调用结果，这就是最终 MCP 协议层的数据结构；使用此返回值类型，你可以直接在 MCP 协议层控制返回给 AI 的数据
- `CallToolResult<T>`: 带有结构化数据类型的工具调用结果，通过 `CallToolResult.FromResult(result)` 方法创建实例，`T` 是任意可被 JSON 序列化的类型；使用此返回值类型，你可以在保证不破坏结构化返回值功能的同时，仍然具备在 MCP 协议层控制返回给 AI 的数据的能力

**特别的**，当返回值是可被 JSON 序列化的对象时，按 MCP 协议规范，我们会返回结构化数据，并在普通字符串返回值中也包含此数据的 JSON 序列化字符串（以供兼容）。同时此工具还会被标记为「具有结构化返回值」。

方法可以是同步或异步的：

- 支持上述所有种类的同步返回值
- 支持 `Task`、`Task<T>`、`ValueTask` 和 `ValueTask<T>` 的异步返回值

### 类型多态

允许方法参数和返回值使用接口或抽象类类型，但需要标注全部可能的具体实现类型：

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Foo), typeDiscriminator: "foo")]
[JsonDerivedType(typeof(Bar), typeDiscriminator: "bar")]
public interface IFooBar
{
    [JsonPropertyName("name")]
    string? Name { get; init; }
}

public class Foo : IFooBar
{
    public string? Name { get; init; }

    [JsonPropertyName("fooValue")]
    public int FooValue { get; init; }
}

public class Bar : IFooBar
{
    public string? Name { get; init; }

    [JsonPropertyName("barValue")]
    public string? BarValue { get; init; }
}
```

请注意，Json 序列化器必须标注 `AllowOutOfOrderMetadataProperties`，因为 AI 不一定会按顺序传：

```csharp
[JsonSerializable(typeof(IFooBar))]
[JsonSourceGenerationOptions(
    AllowOutOfOrderMetadataProperties = true)]
internal partial class McpToolJsonContext : JsonSerializerContext;
```
