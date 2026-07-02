# 编译期 JSON Schema 生成

本库提供了 `[GenerateJsonSchema]` 特性，可以在**编译期**生成 JSON Schema，既可以获得编译时才有的信息（如 XML 文档注释），也无需运行时反射。

[MCP 工具](Tools.md)的 Input/Output Schema 使用与本机制完全相同的 Schema 生成逻辑，生成的 JSON Schema 完全相同。

## 基本用法

### 第一步：标记 JsonSerializerContext

在你的 `JsonSerializerContext` 派生类上添加 `[GenerateJsonSchema]` 特性：

```csharp
[GenerateJsonSchema]
[JsonSerializable(typeof(MyModel))]
[JsonSerializable(typeof(MyOtherModel))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
internal partial class MyJsonContext : JsonSerializerContext;
```

### 第二步：调用扩展方法

编译后，每个 `[JsonSerializable]` 标注的类型都会自动生成一个 `GetCompilerGeneratedJsonSchema()` 扩展方法：

```csharp
// 直接通过 JsonTypeInfo 调用
JsonElement schema = MyJsonContext.Default.MyModel.GetCompilerGeneratedJsonSchema();

// 得到的 schema 是一个标准的 JSON Schema 对象
Console.WriteLine(schema.GetProperty("type").GetString());   // "object"
Console.WriteLine(schema.GetProperty("properties"));         // 各属性的 Schema
Console.WriteLine(schema.GetProperty("required"));           // 必需属性列表
```

## 自动生成的 Schema 内容

以下示例模型涵盖了本机制支持的全部能力——required、nullable、enum、嵌套类型、数组、`[JsonPropertyName]` 等：

```csharp
/// <summary>
/// 订单信息
/// </summary>
public record Order
{
    /// <summary>
    /// 订单编号
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 订单金额（可选）
    /// </summary>
    public decimal? Amount { get; init; }

    /// <summary>
    /// 订单状态
    /// </summary>
    public OrderStatus Status { get; init; }

    /// <summary>
    /// 商品明细
    /// </summary>
    public required List<OrderItem> Items { get; init; }
}

/// <summary>
/// 订单状态
/// </summary>
public enum OrderStatus
{
    /// <summary>待处理</summary>
    Pending,
    /// <summary>已发货</summary>
    Shipped,
    /// <summary>已完成</summary>
    Done,
}

/// <summary>
/// 订单项
/// </summary>
public record OrderItem
{
    /// <summary>
    /// 商品名
    /// </summary>
    [JsonPropertyName("product_name")]
    public required string ProductName { get; init; }

    /// <summary>
    /// 数量
    /// </summary>
    public int Quantity { get; init; }
}
```

```csharp
var schema = MyJsonContext.Default.Order.GetCompilerGeneratedJsonSchema();
```

生成的 JSON Schema：

```json
{
  "type": "object",
  "description": "订单信息",
  "properties": {
    "id": { "type": "string", "description": "订单编号" },
    "amount": { "type": ["number", "null"], "description": "订单金额（可选）" },
    "status": {
      "type": "string",
      "description": "订单状态",
      "enum": ["Pending", "Shipped", "Done"]
    },
    "items": {
      "type": "array",
      "description": "商品明细",
      "items": {
        "type": "object",
        "description": "订单项",
        "properties": {
          "product_name": { "type": "string", "description": "商品名" },
          "quantity": { "type": "integer", "description": "数量" }
        },
        "required": ["product_name", "quantity"]
      }
    }
  },
  "required": ["id", "items"]
}
```

逐属性解读：

| 属性           | 体现的机制                                                                                 |
| -------------- | ------------------------------------------------------------------------------------------ |
| `id`           | `required` 修饰符 → 加入 `required` 列表；XML 注释 → `description`                         |
| `amount`       | `decimal?` 可空值类型 → `type` 为 `["number", "null"]`                                     |
| `status`       | 枚举类型 → 生成 `enum` 数组；枚举成员的 `<summary>` 自动收集                               |
| `items`        | 嵌套类型 → `items` 中递归生成 `OrderItem` 的完整 Schema                                    |
| `product_name` | `[JsonPropertyName("product_name")]` → Schema 属性名使用 `product_name` 而非 `ProductName` |
| `quantity`     | 非空值类型 → 自动加入 `required` 列表                                                      |

> **递归特性**：XML 注释的提取是递归的，任意深度的嵌套属性的 `<summary>` 都会被提取。此外，如果类型来自基础库、NuGet 包或外部 dll，只要编译期可获取其 XML 文档，注释同样能被提取到 Schema 中。

## 典型场景：按需获取 Schema，避免智能体上下文污染

当你需要支持几十种不同类型的对象时，如果直接使用[类型多态](Polymorphism.md)，MCP 工具的 Schema 会包含所有子类型的完整定义，急剧膨胀，严重污染智能体的上下文窗口。

使用本机制，你可以将参数类型设为 `JsonElement`，然后通过额外的工具按需提供具体类型的 Schema。

### 问题：多态导致 Schema 膨胀

例如，如下 MCP 工具，`EventBase` 有数十种子类型。MCP 工具会收集所有的子类型作为一整个 JSON Schema，导致 Schema 急剧膨胀。

```csharp
[McpServerTool]
public string HandleEvent(EventBase evt) { ... }
```

### 解决：JsonElement + 按需 Schema 工具

步骤一：将参数类型改为 `JsonElement`，避开子类型 Schema 生成：

```csharp
// 只暴露一个通用的 JsonElement，避开大量类型的类型多态
[McpServerTool]
public string HandleEvent(JsonElement evt) { ... }
```

步骤二：使用 `[GenerateJsonSchema]` 为每种具体事件类型生成 Schema：

```csharp
[GenerateJsonSchema]
[JsonSerializable(typeof(ClickEvent))]
[JsonSerializable(typeof(KeyPressEvent))]
[JsonSerializable(typeof(ScrollEvent))]
// ... 更多事件类型
internal partial class EventSchemaContext : JsonSerializerContext;
```

步骤三：新增一个 MCP 工具，让智能体按需查询特定类型的 Schema：

```csharp
/// <summary>
/// 获取指定事件类型的 JSON Schema。
/// 当你需要构建某种事件时，先调用此工具了解其字段结构。
/// </summary>
/// <param name="eventType">事件类型名称，如 "ClickEvent"、"KeyPressEvent" 等</param>
[McpServerTool(ReadOnly = true)]
public JsonElement GetEventSchema(string eventType)
{
    return eventType switch
    {
        nameof(ClickEvent) => EventSchemaContext.Default.ClickEvent
            .GetCompilerGeneratedJsonSchema(),
        nameof(KeyPressEvent) => EventSchemaContext.Default.KeyPressEvent
            .GetCompilerGeneratedJsonSchema(),
        nameof(ScrollEvent) => EventSchemaContext.Default.ScrollEvent
            .GetCompilerGeneratedJsonSchema(),
        _ => throw new ArgumentException($"未知事件类型: {eventType}"),
    };
}
```

### 效果

- `HandleEvent` 的 Schema 干净简洁，只暴露一个 `JsonElement` 参数
- 智能体需要了解某种事件的结构时，调用 `GetEventSchema` 按需获取
