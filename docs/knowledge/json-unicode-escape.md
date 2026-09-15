# System.Text.Json 非 ASCII 字符被转义为 \uXXXX 的问题与解决方案

System.Text.Json 默认将所有非 ASCII 字符转义为 `\uXXXX` 形式：

```json
{"name":"\u5434\u519c","title":"\u5468\u65E5"}
```

这是合法的 JSON，标准解析器能正确还原，但大语言模型（LLM）有时会把 `\uXXXX` 当作字面字符串而非 Unicode 码位处理，导致对其中人名等内容的理解出错。

**根本原因**：`JavaScriptEncoder.Default` 是 HTML 安全编码器，对所有非 ASCII 字符一律转义。改用 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` 可让非 ASCII 字符原样输出。

> **注意**：`UnsafeRelaxedJsonEscaping` 不转义 HTML 敏感字符（`<` `>` `&` `'`），因此不能将其输出内嵌到 HTML 页面或 `<script>` 标签中；仅适用于接收方以 UTF-8 JSON 解析（如 `Content-Type: application/json; charset=utf-8`）的场景。

---

## 非 AOT 用法

直接在 `JsonSerializerOptions` 上设置编码器：

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;

var options = new JsonSerializerOptions
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

string json = JsonSerializer.Serialize(obj, options);
```

---

## AOT / Source Generation 用法

`[JsonSourceGenerationOptions]` 没有 `Encoder` 属性，无法在特性上直接配置。解决办法是在 `JsonSerializerContext` 子类的**静态构造函数**中覆写 `Default`——将源生成器生成的 `s_defaultOptions`（含特性中声明的所有选项）复制一份，再叠加编码器：

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(MyData))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
internal partial class MyJsonContext : JsonSerializerContext
{
    static MyJsonContext()
    {
        Default = new MyJsonContext(new JsonSerializerOptions(s_defaultOptions)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }
}
```

`s_defaultOptions` 是源生成器在分部类中生成的私有静态字段；静态字段初始化器先于静态构造函数执行，因此静态构造函数中对 `Default` 的赋值是最终生效值。此方式仍使用源生成器元数据，不引入反射，完全 AOT 兼容。

---

## 示例输出

```csharp
var data = new Schedule("吴农", "周日");
string json = JsonSerializer.Serialize(data, MyJsonContext.Default.Schedule);
// 输出：{"person":"吴农","dayName":"周日"}
// 默认：{"person":"\u5434\u519c","dayName":"\u5468\u65E5"}
```

---

## JsonNode.ToJsonString() 用法

使用 `JsonObject` / `JsonArray` 动态构造 JSON 时，调用 `ToJsonString()` 同样需要传入带编码器的选项。在程序集内定义一个静态共享实例供所有动态 JSON 构造复用：

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;

internal static class MyJsonOptions
{
    internal static readonly JsonSerializerOptions RelaxedEncoding = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
```

使用时传入选项：

```csharp
var result = new JsonObject
{
    ["name"] = "吴农",
    ["day"] = "周日",
};
return result.ToJsonString(MyJsonOptions.RelaxedEncoding);
// 输出：{"name":"吴农","day":"周日"}
// 默认：{"name":"\u5434\u519c","day":"\u5468\u65E5"}
```

---

参考：
- [dotnet/runtime#94135](https://github.com/dotnet/runtime/issues/94135)
- [How to customize character encoding with System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/character-encoding)
