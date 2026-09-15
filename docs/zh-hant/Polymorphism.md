# 多型型別

當工具的參數或傳回值需要支援多種子型別時（例如處理不同形狀的訊息、事件、命令等），可以使用 C# 的多型型別。本庫支援透過 System.Text.Json 的 `[JsonPolymorphic]` / `[JsonDerivedType]` 設定型別多型。

多型同時適用於工具的輸入參數和傳回值。

## 定義多型型別

一個使用型別多型的範例：

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

- `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` 指定鑑別器 JSON 屬性名稱。
- `[JsonDerivedType(typeof(...), "...")]` 宣告每個子型別及其鑑別器值。
- 需要確保 `JsonSerializerContext` 能提供該多型型別的序列化資訊；直接作為工具參數或傳回值時，應註冊多型基底型別。

## 輸入多型

工具方法的參數可以直接宣告為多型基底型別，例如：

```csharp
/// <summary>
/// 測試多型參數的工具方法
/// </summary>
/// <param name="param">多型參數</param>
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

> **⚠ 重要**：如果用戶端傳入的 JSON 缺少鑑別器屬性或鑑別器值不符合任何 `[JsonDerivedType]` 宣告的子型別，本庫會擲出 `McpToolMissingRequiredTypeDiscriminatorException` 並向用戶端報告錯誤，錯誤訊息包含期望的鑑別器屬性名稱和有效值列表。

此外，也支援輸入參數中的任何一個屬性或遞迴的任意子孫屬性使用多型。

## 輸出多型

方法的傳回值同樣可以是多型型別。

```csharp
/// <summary>
/// 傳回一個多型物件
/// </summary>
[McpServerTool(ReadOnly = true)]
public PolymorphicBase GetPolymorphicResult()
{
    return new PolymorphicDerivedA { Foo = "result" };
}
```

對於非空自訂物件傳回值，預設會產生包含多型的 `outputSchema` 和 `structuredContent`。
