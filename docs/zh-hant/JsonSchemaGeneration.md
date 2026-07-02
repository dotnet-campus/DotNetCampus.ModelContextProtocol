# 編譯期 JSON Schema 生成

本程式庫提供了 `[GenerateJsonSchema]` 特性，可以在**編譯期**產生 JSON Schema，既可以獲得編譯時才有的資訊（如 XML 文件註解），也無需執行時反射。

[MCP 工具](Tools.md)的 Input/Output Schema 使用與本機制完全相同的 Schema 產生邏輯，產生的 JSON Schema 完全相同。

## 基本用法

### 第一步：標記 JsonSerializerContext

在你的 `JsonSerializerContext` 衍生類別上新增 `[GenerateJsonSchema]` 特性：

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

> 注意：JSON Schema 的屬性名產生規則與 MCP 工具保持一致：優先使用 `[JsonPropertyName]`，否則使用 camelCase。目前不會讀取 `JsonSourceGenerationOptions` 中的 `PropertyNamingPolicy` 或 `DictionaryKeyPolicy`。

### 第二步：呼叫擴充方法

編譯後，每個 `[JsonSerializable]` 標注的型別都會自動產生一個 `GetCompilerGeneratedJsonSchema()` 擴充方法：

```csharp
// 直接透過 JsonTypeInfo 呼叫
JsonElement schema = MyJsonContext.Default.MyModel.GetCompilerGeneratedJsonSchema();

// 得到的 schema 是一個標準的 JSON Schema 物件
Console.WriteLine(schema.GetProperty("type").GetString());   // "object"
Console.WriteLine(schema.GetProperty("properties"));         // 各屬性的 Schema
Console.WriteLine(schema.GetProperty("required"));           // 必要屬性列表
```

## 自動產生的 Schema 內容

以下範例模型涵蓋了本機制支援的全部能力——required、nullable、enum、巢狀型別、陣列、`[JsonPropertyName]` 等：

```csharp
/// <summary>
/// 訂單資訊
/// </summary>
public record Order
{
    /// <summary>
    /// 訂單編號
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 訂單金額（可選）
    /// </summary>
    public decimal? Amount { get; init; }

    /// <summary>
    /// 訂單狀態
    /// </summary>
    public OrderStatus Status { get; init; }

    /// <summary>
    /// 商品明細
    /// </summary>
    public required List<OrderItem> Items { get; init; }
}

/// <summary>
/// 訂單狀態
/// </summary>
public enum OrderStatus
{
    /// <summary>待處理</summary>
    Pending,
    /// <summary>已出貨</summary>
    Shipped,
    /// <summary>已完成</summary>
    Done,
}

/// <summary>
/// 訂單項目
/// </summary>
public record OrderItem
{
    /// <summary>
    /// 商品名
    /// </summary>
    [JsonPropertyName("product_name")]
    public required string ProductName { get; init; }

    /// <summary>
    /// 數量
    /// </summary>
    public int Quantity { get; init; }
}
```

```csharp
var schema = MyJsonContext.Default.Order.GetCompilerGeneratedJsonSchema();
```

產生的 JSON Schema：

```json
{
  "type": "object",
  "properties": {
    "id": { "type": "string", "description": "訂單編號" },
    "amount": { "type": ["number", "null"], "description": "訂單金額（可選）" },
    "status": {
      "type": "string",
      "description": "訂單狀態\nPending: 待處理\nShipped: 已出貨\nDone: 已完成",
      "enum": ["Pending", "Shipped", "Done"]
    },
    "items": {
      "type": "array",
      "description": "商品明細",
      "items": {
        "type": "object",
        "properties": {
          "product_name": { "type": "string", "description": "商品名" },
          "quantity": { "type": "integer", "description": "數量" }
        },
        "required": ["product_name"]
      }
    }
  },
  "required": ["id", "items"]
}
```

逐屬性解讀：

| 屬性           | 體現的機制                                                                                  |
| -------------- | ------------------------------------------------------------------------------------------ |
| `id`           | `required` 修飾詞 → 加入 `required` 列表；XML 註解 → `description`                          |
| `amount`       | `decimal?` 可空值型別 → `type` 為 `["number", "null"]`                                     |
| `status`       | 列舉型別 → 產生 `enum` 陣列；列舉成員的 `<summary>` 會追加到 `description`                  |
| `items`        | 巢狀型別 → `items` 中遞迴產生 `OrderItem` 的完整 Schema                                    |
| `product_name` | `[JsonPropertyName("product_name")]` → Schema 屬性名使用 `product_name` 而非 `ProductName` |
| `quantity`     | 未使用 `required` 修飾詞 → 不加入 `required` 列表                                           |

> **遞迴特性**：XML 註解的提取是遞迴的，任意深度的巢狀屬性的 `<summary>` 都會被提取。目前物件型別自身的 `<summary>` 不會寫入該物件 Schema 的 `description`。

## 典型場景：按需取得 Schema，避免智慧體上下文污染

當你需要支援幾十種不同型別的物件時，如果直接使用[型別多型](Polymorphism.md)，MCP 工具的 Schema 會包含所有子型別的完整定義，急劇膨脹，嚴重污染智慧體的上下文視窗。

使用本機制，你可以將參數型別設為 `JsonElement`，然後透過額外的工具按需提供具體型別的 Schema。

### 問題：多型導致 Schema 膨脹

例如，如下 MCP 工具，`EventBase` 有數十種子型別。MCP 工具會收集所有的子型別作為一整個 JSON Schema，導致 Schema 急劇膨脹。

```csharp
[McpServerTool]
public string HandleEvent(EventBase evt) { ... }
```

### 解決：JsonElement + 按需 Schema 工具

步驟一：將參數型別改為 `JsonElement`，避開子型別 Schema 產生：

```csharp
// 只暴露一個通用的 JsonElement，避開大量型別的型別多型
[McpServerTool]
public string HandleEvent(JsonElement evt) { ... }
```

步驟二：使用 `[GenerateJsonSchema]` 為每種具體事件型別產生 Schema：

```csharp
[GenerateJsonSchema]
[JsonSerializable(typeof(ClickEvent))]
[JsonSerializable(typeof(KeyPressEvent))]
[JsonSerializable(typeof(ScrollEvent))]
// ... 更多事件型別
internal partial class EventSchemaContext : JsonSerializerContext;
```

步驟三：新增一個 MCP 工具，讓智慧體按需查詢特定型別的 Schema：

```csharp
/// <summary>
/// 取得指定事件型別的 JSON Schema。
/// 當你需要構建某種事件時，先呼叫此工具了解其欄位結構。
/// </summary>
/// <param name="eventType">事件型別名稱，如 "ClickEvent"、"KeyPressEvent" 等</param>
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
        _ => throw new ArgumentException($"未知事件型別: {eventType}"),
    };
}
```

### 效果

- `HandleEvent` 的 Schema 乾淨簡潔，只暴露一個 `JsonElement` 參數
- 智慧體需要了解某種事件的結構時，呼叫 `GetEventSchema` 按需取得
