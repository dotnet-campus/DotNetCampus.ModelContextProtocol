# 多态类型

当工具的参数或返回值需要支持多种子类型时（例如处理不同形状的消息、事件、命令等），可以使用 C# 的多态类型。本库支持通过 System.Text.Json 的 `[JsonPolymorphic]` / `[JsonDerivedType]` 设置类型多态。

多态同时适用于工具的输入参数和返回值。

## 定义多态类型

一个使用类型多态的示例：

```csharp
[JsonDerivedType(typeof(PolymorphicDerivedA), "a")]
[JsonDerivedType(typeof(PolymorphicDerivedB), "b")]
[JsonDerivedType(typeof(PolymorphicDerivedC), "c")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
public abstract record PolymorphicBase
{
}

public record PolymorphicDerivedA : PolymorphicBase
{
    public string? Foo { get; init; }
}

public record PolymorphicDerivedB : PolymorphicBase
{
    public int? Bar { get; init; }
}

public record PolymorphicDerivedC : PolymorphicBase
{
    public JsonElement? Baz { get; init; }
}
```

其中：

- `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` 指定鉴别器 JSON 属性名。
- `[JsonDerivedType(typeof(...), "...")]` 声明每个子类型及其鉴别器值。
- 需要确保 JsonSerializerContext 能提供该多态类型的序列化信息；直接作为工具参数或返回值时，应注册多态基类。

## 输入多态

工具方法的参数可以直接声明为多态基类类型，例如：

```csharp
/// <summary>
/// 测试多态参数的工具方法
/// </summary>
/// <param name="param">多态参数</param>
[McpServerTool(ReadOnly = true)]
public string TestPolymorphicParameter(PolymorphicBase param)
{
    return param switch
    {
        PolymorphicDerivedA a => $"Received DerivedA with Foo = {a.Foo}",
        PolymorphicDerivedB b => $"Received DerivedB with Bar = {b.Bar}",
        _ => "Unknown type",
    };
}
```

> **⚠ 重要**：如果客户端传入的 JSON 缺少鉴别器属性或鉴别器值不匹配任何 `[JsonDerivedType]` 声明的子类型，本库会抛出 `McpToolMissingRequiredTypeDiscriminatorException` 并向客户端报告错误，错误消息包含期望的鉴别器属性名和有效值列表。

此外，也支持输入参数中的任何一个属性或递归的任意子孙属性使用多态。

## 输出多态

方法的返回值同样可以是多态类型。

```csharp
/// <summary>
/// 返回一个多态对象
/// </summary>
[McpServerTool(ReadOnly = true)]
public PolymorphicBase GetPolymorphicResult()
{
    return new PolymorphicDerivedA { Foo = "result" };
}
```

对于非空自定义对象返回值，默认会生成包含多态的 `outputSchema` 和 `structuredContent`
