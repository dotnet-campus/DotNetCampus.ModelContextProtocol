# 快速使用

## MCP 工具方法声明

```csharp
public class SampleTools
{
    /// <summary>
    /// [ForAI] 用于给 AI 调试使用的工具，原样返回一些信息
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

- 任意可被 JSON 反序列化的类型
- `CancellationToken`: 表示取消令牌
- `IMcpCallToolContext`: 表示当前工具方法的上下文信息

方法的返回值可以是以下类型：

- `string`: 表示返回给 AI 的字符串（通常是可被 AI 理解的自然语言）
- `CallToolResult`: 通用的工具调用结果，这就是最终 MCP 协议层的数据结构
- `CallToolResult<T>`: 带有结构化数据类型的工具调用结果，通过 `CallToolResult.FromResult(result)` 方法创建实例，`T` 是任意可被 JSON 序列化的类型

方法可以是同步或异步的：

- 支持上述所有种类的同步返回值
- 支持 `Task<T>` 和 `ValueTask<T>` 的异步返回值

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
