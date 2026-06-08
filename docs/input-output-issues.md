# MCP 工具输入输出 Schema 与 JSON 序列化一致性问题

## 1. 文档目的

本文是一次开发任务交接文档，记录 MCP 工具输入参数、输出值、JSON Schema、源生成代码和 `System.Text.Json` 序列化契约之间已经发现的问题。

下一次新会话开始实现前，应先阅读本文，再检查文中引用的源码。不要只修复枚举默认值 `"1"`；该问题只是同一类架构不一致中最明显的一个表现。

本文区分三类结论：

- **已确认缺陷**：已经通过源码或生成代码确认。
- **已确认设计不一致**：当前行为不一定立刻报错，但 Schema 与实际 wire format 可能分离。
- **待决策项**：修复前需要确定兼容性和公开契约。

本文不宣称已经完成整个 `System.Text.Json` 契约的穷尽审计。最后列出了应继续检查的相邻风险。

## 2. 问题背景

本库使用源生成器分析带有 `[McpServerTool]` 的方法，并在编译期生成：

- 工具名称和描述。
- `inputSchema`。
- 可选的 `outputSchema`。
- 输入参数提取和反序列化代码。
- 返回值到 `CallToolResult` 的转换代码。

当前 Schema 和运行时 JSON 行为来自两套不同的信息源：

1. **编译期 Schema**
   - 由 Analyzer 直接分析 Roslyn 符号。
   - 属性名默认由本库转换成 camelCase。
   - 枚举 Schema 固定使用字符串类型和 CLR 枚举成员名。
   - Schema 生成时拿不到服务器实际配置的业务 `JsonSerializerContext`。

2. **运行时序列化和反序列化**
   - 使用 `McpServerBuilder.WithJsonSerializer(...)` 传入的业务 `JsonSerializerContext`。
   - 受 `JsonSourceGenerationOptions`、`JsonPropertyName`、`JsonStringEnumMemberName`、类型转换器等影响。

根本风险是：

```text
编译期声明给客户端的 JSON Schema
可能不等于
运行时真正接受或产生的 JSON
```

当前 `IMcpServerTool.GetToolDefinition` 只接收 `CompiledSchemaJsonContext`，工具列表请求也固定用 `CompiledSchemaJsonContext.Default`。业务 `JsonSerializerContext` 没有参与 Schema 构造：

- `src/DotNetCampus.ModelContextProtocol/Servers/IMcpServerTool.cs`
- `src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs:213`
- `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerToolSourceBuilder.cs:19-30`

## 3. 当前公开参数分类

`ToolParameterType` 有 6 种值：

1. `Parameter`
2. `InputObject`
3. `Context`
4. `Injected`
5. `JsonElement`
6. `CancellationToken`

源码：

- `src/DotNetCampus.ModelContextProtocol/CompilerServices/ToolParameterAttribute.cs`
- `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/ToolSymbolExtensions.cs:41-75`

文档应使用这 6 种值作为参数“来源/用途”的主分类。以下内容不应被描述成新的 `ToolParameterType`：

- 可空和非可空 DI：都是 `Injected`，只是服务缺失时行为不同。
- 枚举、字符串、数字、对象、集合：都是 `Parameter` 或 `InputObject` 内部的 JSON 数据形态。
- 必需参数和带默认值参数：是 required/default 行为，不是新的参数类型。

建议文档采用两层结构：

1. 第一层按 6 种 `ToolParameterType` 说明参数来源和是否进入 Schema。
2. 第二层在 `Parameter`/`InputObject` 下说明 string、boolean、number、enum、object、array、dictionary、nullable 等 JSON Schema 行为。

## 4. 问题清单

### 4.1 已确认缺陷：枚举默认值生成成底层数字字符串

示例：

```csharp
public enum EchoOptions
{
    PlainText,
    JsonObject,
}

public CallToolResult Echo(
    string text,
    EchoOptions options = EchoOptions.JsonObject)
```

当前生成结果：

```csharp
[ "options" ] = new CompiledJsonSchema
{
    Type = JsonSerializer.SerializeToElement("string", jsonContext.String),
    Default = JsonSerializer.SerializeToElement("1", jsonContext.String),
    Enum = [ "PlainText", "JsonObject" ],
},
```

Schema 自相矛盾：

```text
default = "1"
enum = ["PlainText", "JsonObject"]
```

`"1"` 不属于允许值。

直接原因：

- 枚举在 Schema 中映射为 `string`：
  `src/DotNetCampus.ModelContextProtocol.Analyzer/CodeAnalysis/JsonSchemaType.cs:116`
- `IParameterSymbol.ExplicitDefaultValue` 对枚举给出底层常量。
- 默认值生成逻辑按照 Schema 的 `String` 分支执行 `defaultValue.ToString()`：
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/JsonPropertySchemaInfo.cs:321-339`

修复要求：

- 枚举必须单独处理，不得复用普通 string 默认值分支。
- 生成的非空默认值必须满足 `default` 属于 `enum`。
- 测试必须覆盖底层值为 0、非 0 和自定义数值的成员。

### 4.2 已确认缺陷：枚举 Schema 不遵循 `JsonStringEnumMemberName`

当前 `EnumJsonValueInfo` 直接使用 `IFieldSymbol.Name`：

- `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/EnumJsonValueInfo.cs:31-45`
- `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/JsonPropertySchemaInfo.cs:135-145`

例如：

```csharp
public enum EchoOptions
{
    [JsonStringEnumMemberName("jsonObject")]
    JsonObject,
}
```

运行时字符串枚举转换器可以接受或写出 `"jsonObject"`，但当前 Schema 仍声明 `"JsonObject"`。

该问题同时影响：

- 直接枚举参数。
- 可空枚举参数。
- 枚举集合的 `items.enum`。
- 输入对象中的枚举属性。
- 结构化输出对象中的枚举属性。
- 枚举默认值。
- 自动附加到描述中的枚举值提示。

### 4.3 已确认设计不一致：枚举 Schema 固定为字符串，但运行时未强制使用字符串枚举

当前生成器固定输出：

```json
{
  "type": "string",
  "enum": ["PlainText", "JsonObject"]
}
```

运行时反序列化却使用业务 `JsonSerializerContext`：

- `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerToolSourceBuilder.cs:202-205`
- `src/DotNetCampus.ModelContextProtocol/CompilerServices/CompilerExtensions.cs:73-81`

如果业务上下文没有有效的字符串枚举转换器，Schema 要求客户端发送字符串，但运行时可能按数字枚举处理。

当前文档仅把 `UseStringEnumConverter = true` 写成“建议设置”，不足以表达契约要求：

- `docs/zh-hans/QuickStart.md:39-49`

注意：要求不应机械表述成“必须设置 `UseStringEnumConverter = true`”，因为枚举类型自身的 `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` 也可以提供字符串契约。准确要求应是：

> 对所有出现在工具 JSON payload 中的枚举，实际 `JsonTypeInfo` 必须使用与 Schema 一致的字符串 wire format。

### 4.4 已确认缺陷：直接枚举返回值绕过 JSON 枚举契约

普通枚举返回值当前使用 `ToString()`：

- `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerToolSourceBuilder.cs:360-391`

枚举集合也逐项使用字符串插值，本质仍是 `ToString()`：

- `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerToolSourceBuilder.cs:286-290`

因此：

```csharp
[JsonStringEnumMemberName("jsonObject")]
JsonObject
```

可能出现：

```text
输入 Schema / 对象 JSON：jsonObject
直接枚举返回文本：JsonObject
```

需要决定直接枚举返回究竟是：

1. 普通文本展示值，明确固定使用 CLR `ToString()`；或
2. JSON wire value，必须通过实际枚举 `JsonTypeInfo` 序列化。

推荐采用第 2 种，确保输入、对象输出、直接输出和集合输出使用同一 wire value。若序列化后得到 JSON 字符串，需要提取字符串内容，而不是把带引号的 JSON 文本直接放进 `TextContentBlock`。

### 4.5 已确认设计不一致：对象属性 Schema 固定 camelCase，不遵循业务命名策略

顶级方法参数名和对象属性名目前由生成器自行决定：

- 方法参数默认 camelCase，可由 `[ToolParameter(Name = "...")]` 覆盖：
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/ToolSymbolExtensions.cs:92-110`
- 对象属性默认 camelCase，可由 `[JsonPropertyName]` 覆盖：
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/ToolSymbolExtensions.cs:139-153`

顶级普通方法参数不存在对象属性序列化过程。生成器用同一个名称生成 Schema 并从 `jsonArguments` 取值，因此这一层内部是一致的。

真正的不一致发生在对象内部：

- 普通对象参数。
- `[ToolParameter(Type = InputObject)]` 的整个输入对象。
- 结构化输出对象。
- 对象集合元素。
- 嵌套对象。

例如业务上下文配置：

```csharp
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
```

可能得到：

```text
Schema 属性名：someValue
运行时 JSON 属性名：some_value
```

`[JsonPropertyName]` 当前在 Schema 和运行时两边都能生效，是已支持的显式覆盖机制。

### 4.6 已确认设计不一致：Schema 不会自动遵循 `JsonSourceGenerationOptions`

业务上下文的这些设置可能影响实际 JSON：

- `PropertyNamingPolicy`
- `UseStringEnumConverter`
- 未来或其他可影响 wire shape 的序列化选项

但 Analyzer 生成 Schema 时不知道服务器最终会传入哪个 `JsonSerializerContext`。同一个工具类型理论上还可以注册到使用不同上下文的不同服务器。

当前架构中：

- Schema 是编译期硬编码到桥接类的。
- `GetToolDefinition` 只接收 `CompiledSchemaJsonContext`。
- `McpServerRequestHandlers` 获取工具列表时没有把业务上下文传给工具。

所以“让现有编译期 Schema 自动完整遵循任意业务 `JsonSourceGenerationOptions`”不是局部改动，必须先选择架构方案，见第 7 节。

### 4.7 已确认缺陷：多态类型在未指定 discriminator 时自行发明类型名

`PolymorphicTypeInfo` 在 `[JsonDerivedType(typeof(Foo))]` 没有显式 discriminator 时使用 `derivedType.Name`：

- `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/PolymorphicTypeInfo.cs:67-82`

随后 Schema 强制添加鉴别器属性和 `const`：

- `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerToolSourceBuilder.cs:87-93`
- `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerToolSourceBuilder.cs:113-143`

这不等同于 `System.Text.Json` 的实际契约。没有显式 type discriminator 的 `[JsonDerivedType]` 不应由本库擅自创造一个 wire discriminator。

修复方向：

- 仅在 `JsonDerivedType` 显式提供 discriminator 时生成 discriminator Schema；或
- 对工具参数要求所有派生类型显式声明 discriminator，并给出编译诊断。

推荐第二种，因为工具输入需要可反序列化、可向模型明确描述的稳定联合类型。

### 4.8 已确认缺陷：整数多态 discriminator 被错误转换为字符串

`JsonDerivedTypeAttribute` 支持字符串或整数 discriminator。当前代码把整数执行 `ToString()`：

- `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/PolymorphicTypeInfo.cs:67-76`

`CompiledJsonSchema.Const` 又被定义成 `string?`：

- `src/DotNetCampus.ModelContextProtocol/CompilerServices/CompiledJsonSchema.cs`

因此整数 discriminator `1` 会被声明成字符串常量 `"1"`，而运行时 JSON 契约要求数字 `1`。

修复要求：

- discriminator value 必须保留 JSON 类型。
- `const` 不能只支持 string，建议改为 `JsonElement?` 或可表达标量的结构。
- 测试字符串和整数 discriminator。

### 4.9 已确认文档问题：参数类型表混合了“参数来源”和“.NET/JSON 类型”

`docs/zh-hans/Tools.md:84-94` 当前表格把以下概念放在同一层：

- `IMcpServerCallToolContext`
- JSON 可序列化类型
- `JsonElement`
- `InputObject`
- 可空 DI
- 非空 DI
- `CancellationToken`

这与公开的 6 个 `ToolParameterType` 不一致，也造成：

- `Injected` 被按可空性拆成两种“参数类型”。
- 枚举被隐藏在“JSON 可序列化类型”中。
- 用户难以理解枚举是 `Parameter` 的一种 Schema 形态，而不是类似 `Injected` 的参数来源。

修复后的文档结构见第 3 节。

### 4.10 已确认文档问题：返回值表把枚举与数值和布尔值混为一类

当前：

- `docs/zh-hans/Tools.md:178-179`
- `docs/zh-hans/Tools.md:183`

表格把枚举与 `int`、`bool` 共同描述为基本类型，虽然运行代码都是 `ToString()`，但 wire 语义不同：

- `int` 返回数字的文本表示。
- `bool` 返回布尔值的文本表示。
- 枚举返回成员名文本，目前更接近字符串枚举。

文档应单列：

- 枚举。
- 可空枚举。
- 枚举集合。

并根据最终实现说明是否遵循 `JsonStringEnumMemberName`。

### 4.11 已确认文档问题：只说“自定义类型”需要 JSON source generation 注册

当前：

- `docs/zh-hans/QuickStart.md:18`
- `docs/zh-hans/Tools.md:290`

实际直接枚举参数也需要业务上下文提供相应 `JsonTypeInfo`。示例工程已经显式注册：

- `samples/DotNetCampus.SampleMcpServer/McpTools/McpToolJsonContext.cs:6`

文档应明确：

- 直接使用的业务枚举需要注册。
- 业务对象、集合及其嵌套类型需要确保上下文能返回对应的 `JsonTypeInfo`。
- 具体应注册根类型还是同时注册元素类型，应以 source-generated context 实测和测试为准，文档不要给出未经测试的过度承诺。

### 4.12 已确认文档问题：`UseStringEnumConverter` 的说明过弱且不完整

当前注释为“建议设置：MCP 协议主流实现都使用字符串枚举”。

更准确的说明应是：

- 本库当前为工具枚举生成字符串 Schema。
- 因此运行时枚举 JSON 契约必须与该 Schema 一致。
- `UseStringEnumConverter = true` 是实现这一契约的常见方式。
- 枚举类型上的 `JsonConverter` 也可能实现该契约。
- 任意自定义转换器是否受支持，要看本库最终选择的架构和诊断策略。

## 5. 枚举特殊边界

以下边界尚未完成实现决策，但修复时必须覆盖。

### 5.1 默认命名风格

建议保持当前兼容行为：

```text
默认 wire value = 原始 C# 枚举成员名
```

例如：

```text
JsonObject -> "JsonObject"
```

不要默认改成 camelCase，原因：

- `UseStringEnumConverter = true` 默认保持成员名。
- `PropertyNamingPolicy` 不控制枚举值。
- 当前直接枚举返回使用 `ToString()`。
- 改成 camelCase 是公开 wire format 的破坏性变更。

显式命名建议遵循：

```text
[JsonStringEnumMemberName] > CLR 成员名
```

### 5.2 枚举别名

例如：

```csharp
enum State
{
    Ready = 1,
    Started = 1,
}
```

`IParameterSymbol.ExplicitDefaultValue` 只能看到底层值 `1`，无法仅凭数值确定默认值源码写的是 `Ready` 还是 `Started`。

可选方案：

1. 从参数默认值语法节点读取实际成员表达式。
2. 规定别名默认值选择第一个声明成员，但这可能改变用户意图。
3. 遇到别名默认值产生诊断，要求用户移除默认值或消除歧义。

推荐优先尝试方案 1，无法可靠解析时使用诊断。

### 5.3 未命名枚举值

例如：

```csharp
EchoOptions options = (EchoOptions)42
```

该默认值无法满足有限字符串枚举 Schema。不得继续生成 `"42"`。

推荐产生编译诊断，说明默认值没有对应的已声明字符串成员。

### 5.4 `[Flags]`

字符串枚举转换器通常允许逗号分隔的组合值，但当前 Schema 的有限 `enum` 列表只包含单个成员，不能表达任意组合。

需选择：

1. `[Flags]` 使用 `type: "string"`，不生成 `enum` 限制，只在描述中列出标志。
2. 生成所有组合值。通常不可取，组合数量指数增长。
3. 暂不支持 `[Flags]` 工具参数并产生诊断。

推荐方案 1，并明确组合字符串格式必须与实际转换器一致。

### 5.5 任意自定义枚举转换器

Analyzer 无法可靠推断任意用户 `JsonConverter` 的 wire value。不要宣称支持所有自定义转换器。

第一阶段建议明确支持：

- 默认 CLR 成员名。
- `JsonStringEnumMemberName`。
- 标准字符串枚举转换器。

对枚举类型上的未知自定义转换器可：

- 产生诊断或警告；或
- 要求用户通过本库新增的显式 Schema/wire-name 配置覆盖。

## 6. 当前隐式命名转换清单

| 位置 | 当前默认规则 | 显式覆盖 | 是否读取业务 JSON 上下文 |
|---|---|---|---|
| 工具名 | 方法名转 snake_case | `McpServerTool.Name` | 否，也不应读取 |
| 顶级工具参数名 | 参数名转 camelCase | `ToolParameter.Name` | 否 |
| 输入/输出对象属性 | 属性名转 camelCase | `JsonPropertyName` | Schema 否；运行时是 |
| 枚举值 | CLR 成员名 | 当前 Schema 不支持 `JsonStringEnumMemberName` | Schema 否；运行时是 |
| 枚举默认值 | 当前错误使用底层数字字符串 | 无 | 否 |
| 多态鉴别属性名 | `JsonPolymorphic.TypeDiscriminatorPropertyName`，默认 `$type` | `JsonPolymorphic` | 通过 Roslyn 特性读取 |
| 多态鉴别值 | `JsonDerivedType`；缺失时当前错误回退类型名 | `JsonDerivedType` | 通过 Roslyn 特性读取 |
| 资源名称 | 方法名转 PascalCase | `McpServerResource.Name` | 否，也不应读取 |
| 默认资源 URI 方法段 | 方法名转 kebab-case | `McpServerResource.UriTemplate` | 否，也不应读取 |
| 默认资源 URI 参数占位 | 原始参数名 | 显式 URI 模板 | 否 |

统一约束不应理解为“所有名称使用同一种大小写”。正确目标是：

- MCP 标识符由 MCP 特性和本库默认规则控制。
- JSON payload 名称和值由一个明确且一致的 JSON 契约控制。
- Schema 与运行时必须使用同一个 wire name。

## 7. 修复后的期望效果

### 7.1 参数文档

- 主表与 6 个 `ToolParameterType` 一一对应。
- `Injected` 只列一次，在说明中区分可空与非空服务缺失行为。
- `Parameter` 和 `InputObject` 下另列 JSON 类型行为。
- 枚举明确描述为字符串 Schema，而不是整数 Schema。

### 7.2 枚举输入

对于：

```csharp
public enum EchoOptions
{
    PlainText,

    [JsonStringEnumMemberName("jsonObject")]
    JsonObject,
}

public void Echo(EchoOptions options = EchoOptions.JsonObject)
```

期望 Schema：

```json
{
  "type": "string",
  "enum": ["PlainText", "jsonObject"],
  "default": "jsonObject"
}
```

并满足：

```text
Schema enum 值
= 默认值使用的值
= 实际反序列化接受的值
= 对象 JSON 序列化写出的值
= 直接枚举返回使用的值（若决定返回 wire value）
```

### 7.3 对象属性

对于任何参与输入或结构化输出的对象：

```text
Schema properties 中的名称
= 实际 JsonTypeInfo 使用的属性名称
```

至少保证：

- 默认 camelCase 契约一致。
- `JsonPropertyName` 一致。
- 若允许其他 `PropertyNamingPolicy`，Schema 必须真实遵循；否则应在构建期或启动时明确拒绝/警告。

### 7.4 多态输入

- discriminator 属性名与实际 JSON 一致。
- discriminator 常量保留 string/int JSON 类型。
- 不凭空创造运行时不存在的 discriminator。
- Schema 列出的每个派生类型都能被实际上下文反序列化。

### 7.5 诊断

对于无法可靠生成一致 Schema 的情况，不应静默输出错误 Schema。应考虑增加 Analyzer 诊断：

- 枚举默认值没有对应成员。
- 枚举别名导致默认值歧义。
- 不支持或无法推断的枚举转换器。
- `[Flags]` 契约未明确。
- 多态派生类型没有显式 discriminator。
- Schema 命名策略与业务上下文不兼容。

诊断编号和严重级别需要在实现时另行设计。

## 8. 架构方案与待决策项

### 8.1 方案 A：固定本库 JSON wire contract

规则示例：

- 对象属性固定 camelCase，允许 `JsonPropertyName`。
- 枚举固定字符串成员名，允许 `JsonStringEnumMemberName`。
- 多态必须使用显式 `JsonPolymorphic`/`JsonDerivedType`。
- 业务上下文必须兼容这些规则。

优点：

- 保留编译期 Schema。
- AOT 友好。
- 改动相对集中。

缺点：

- 不能完整支持任意 `JsonSourceGenerationOptions` 和自定义转换器。
- 需要验证或诊断业务上下文是否兼容。

### 8.2 方案 B：运行时从实际 `JsonTypeInfo` 构造 Schema

工具定义生成时取得服务器实际业务 `JsonSerializerContext`，从 `JsonTypeInfo`/`JsonPropertyInfo` 获取真实名称和契约。

优点：

- Schema 最有机会与实际运行时一致。
- 可以支持更多命名策略和转换器。

缺点：

- 需要修改 `IMcpServerTool.GetToolDefinition` 或工具注册模型。
- 需要设计 `JsonTypeInfo` 到 JSON Schema 的转换。
- 任意转换器仍未必能公开枚举的有限值集合。
- 同一工具在不同服务器上下文中可能产生不同 Schema，需要缓存策略。

### 8.3 方案 C：混合方案

- 编译期生成结构和默认契约。
- 运行时使用实际 `JsonTypeInfo` 修正属性名、枚举值等可获取信息。
- 无法推断时产生诊断或要求显式覆盖。

这是长期最灵活的方案，但复杂度最高。

### 8.4 当前推荐

分两阶段：

1. **第一阶段，修复确定性缺陷**
   - 修复枚举默认值。
   - 支持 `JsonStringEnumMemberName`。
   - 修复直接枚举和枚举集合返回。
   - 修复整数多态 discriminator。
   - 禁止凭空生成 discriminator。
   - 明确并测试固定 camelCase + 显式属性覆盖契约。
   - 更新文档。

2. **第二阶段，评估运行时 Schema**
   - 决定是否支持任意业务命名策略。
   - 若支持，调整 `IMcpServerTool.GetToolDefinition` 和 Schema 构造架构。
   - 若不支持，为不兼容上下文提供可靠诊断。

## 9. 建议实现顺序

1. 为 Analyzer 建立可直接断言生成源码或工具 Schema 的测试基础设施。
2. 用回归测试固定当前 `"1"` 缺陷。
3. 重构 `EnumJsonValueInfo`，至少保存：
   - CLR 成员名。
   - wire name。
   - 底层常量值。
   - 描述。
   - 是否为别名/是否存在歧义所需的信息。
4. 修复枚举默认值并处理未命名值、别名。
5. 支持 `JsonStringEnumMemberName`，同时更新 enum、default 和描述提示。
6. 统一直接枚举及枚举集合返回。
7. 处理 `[Flags]`。
8. 修复多态 discriminator 的缺失和 JSON 类型。
9. 决定对象命名策略架构，并增加对应测试/诊断。
10. 更新简体中文文档。
11. 同步英文和繁体中文文档。
12. 运行完整构建和测试。

## 10. 测试清单

当前测试项目引用 Analyzer 作为编译期 Analyzer，但没有发现专门的 GeneratorDriver、生成源码快照或 Schema 单元测试。下一会话应优先补这一层，避免只通过端到端服务器测试间接验证。

至少需要：

### 10.1 枚举 Schema

- 普通枚举参数。
- 可空枚举参数。
- 枚举集合和数组。
- 对象中的枚举属性。
- `InputObject` 中的枚举属性。
- 结构化输出对象中的枚举属性。
- `JsonStringEnumMemberName`。
- 默认值为第一个成员。
- 默认值为非零成员。
- 自定义底层数值。
- 未命名值。
- 重复底层值别名。
- `[Flags]` 单值和组合值。

### 10.2 枚举运行时

- 字符串枚举输入成功。
- Schema 声明值与实际接受值一致。
- 不兼容数字输入的行为被明确固定。
- 直接枚举返回。
- 可空枚举返回。
- 枚举集合返回。
- 对象中的枚举序列化。

### 10.3 属性命名

- 默认 camelCase。
- `JsonPropertyName`。
- record 主构造函数属性。
- 嵌套对象。
- `InputObject`。
- 结构化输出。
- 非 camelCase `PropertyNamingPolicy` 的支持或诊断行为。

### 10.4 多态

- 字符串 discriminator。
- 整数 discriminator。
- 自定义 discriminator 属性名。
- 缺失显式 discriminator。
- 未知 discriminator。
- Schema 中 `const` 的 JSON 类型。

### 10.5 文档示例

- 文档中的 `EchoOptions` 示例应有可验证测试。
- 文档声明的 Schema 和实际生成结果保持一致。

## 11. 已有证据与复现方式

### 11.1 生成代码复现

使用：

```powershell
dotnet build samples\DotNetCampus.SampleMcpServer\DotNetCampus.SampleMcpServer.csproj `
  --no-restore `
  -p:EmitCompilerGeneratedFiles=true `
  -p:CompilerGeneratedFilesOutputPath=<工作区内临时目录>
```

然后检查生成的：

```text
DotNetCampus.ModelContextProtocol.Generators.McpServerToolGenerator/
DotNetCampus.SampleMcpServer.McpTools/
SimpleTool.Echo.cs
```

已经实测生成：

```csharp
Type = JsonSerializer.SerializeToElement("string", jsonContext.String),
Default = JsonSerializer.SerializeToElement("1", jsonContext.String),
Enum = [ "PlainText", "JsonObject" ],
```

同次构建成功，无编译错误。这说明该问题不会被现有构建流程发现。

### 11.2 关键源码

- 枚举映射为 string：
  `src/DotNetCampus.ModelContextProtocol.Analyzer/CodeAnalysis/JsonSchemaType.cs:110-122`
- 枚举成员提取：
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/EnumJsonValueInfo.cs`
- Schema enum 和描述生成：
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/JsonPropertySchemaInfo.cs:131-145`
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/JsonPropertySchemaInfo.cs:184-209`
- 默认值生成：
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/JsonPropertySchemaInfo.cs:321-339`
- 输入反序列化：
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerToolSourceBuilder.cs:168-208`
- 返回值生成：
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerToolSourceBuilder.cs:281-321`
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerToolSourceBuilder.cs:360-391`
- 属性名生成：
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/ToolSymbolExtensions.cs:92-110`
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/ToolSymbolExtensions.cs:136-154`
- 多态信息：
  `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/PolymorphicTypeInfo.cs`
- Schema 数据结构：
  `src/DotNetCampus.ModelContextProtocol/CompilerServices/CompiledJsonSchema.cs`
- 业务 JSON context 合并：
  `src/DotNetCampus.ModelContextProtocol/CompilerServices/McpJsonContext.cs:15-56`
- 默认内部工具 JSON 配置：
  `src/DotNetCampus.ModelContextProtocol/CompilerServices/McpJsonContext.cs:85-135`

### 11.3 示例证据

- 枚举参数和默认值：
  `samples/DotNetCampus.SampleMcpServer/McpTools/SimpleTool.cs:14-25`
- `EchoOptions`：
  `samples/DotNetCampus.SampleMcpServer/McpTools/SimpleTool.cs:69-85`
- 示例 context 已注册枚举且启用字符串枚举：
  `samples/DotNetCampus.SampleMcpServer/McpTools/McpToolJsonContext.cs:5-19`
- 枚举返回和枚举集合返回：
  `samples/DotNetCampus.SampleMcpServer/McpTools/OutputTool.cs:93-100`
  `samples/DotNetCampus.SampleMcpServer/McpTools/OutputTool.cs:124-131`

### 11.4 文档证据

- 参数分类：
  `docs/zh-hans/Tools.md:76-96`
- 返回值分类：
  `docs/zh-hans/Tools.md:160-190`
- JSON serializer 说明：
  `docs/zh-hans/Tools.md:286-310`
- Quick Start context：
  `docs/zh-hans/QuickStart.md:9-50`

## 12. 下一会话必须阅读的参考资料

### 12.1 仓库内资料

按顺序阅读：

1. 本文：`docs/input-output-issues.md`
2. `docs/zh-hans/Tools.md`
3. `docs/zh-hans/QuickStart.md`
4. `src/DotNetCampus.ModelContextProtocol/CompilerServices/ToolParameterAttribute.cs`
5. `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/ToolSymbolExtensions.cs`
6. `src/DotNetCampus.ModelContextProtocol.Analyzer/CodeAnalysis/JsonSchemaTypeInfo.cs`
7. `src/DotNetCampus.ModelContextProtocol.Analyzer/CodeAnalysis/JsonSchemaType.cs`
8. `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/EnumJsonValueInfo.cs`
9. `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/JsonPropertySchemaInfo.cs`
10. `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/McpServerToolGeneratingModel.cs`
11. `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/SourceBuilders/McpServerToolSourceBuilder.cs`
12. `src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/Models/PolymorphicTypeInfo.cs`
13. `src/DotNetCampus.ModelContextProtocol/CompilerServices/CompiledJsonSchema.cs`
14. `src/DotNetCampus.ModelContextProtocol/CompilerServices/McpJsonContext.cs`
15. `src/DotNetCampus.ModelContextProtocol/Servers/IMcpServerTool.cs`
16. `src/DotNetCampus.ModelContextProtocol/Servers/McpServerRequestHandlers.cs`
17. `samples/DotNetCampus.SampleMcpServer/McpTools/SimpleTool.cs`
18. `samples/DotNetCampus.SampleMcpServer/McpTools/McpToolJsonContext.cs`
19. `samples/DotNetCampus.SampleMcpServer/McpTools/OutputTool.cs`
20. `samples/DotNetCampus.SampleMcpServer/McpTools/PolymorphicTool.cs`

### 12.2 官方资料

- System.Text.Json 属性命名和枚举命名：
  https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/customize-properties
- System.Text.Json 多态序列化：
  https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/polymorphism
- `JsonStringEnumMemberNameAttribute`：
  https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonstringenummembernameattribute
- `JsonSourceGenerationOptionsAttribute`：
  https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonsourcegenerationoptionsattribute
- JSON Schema Draft 2020-12：
  https://json-schema.org/draft/2020-12
- MCP Tools 规范：
  https://modelcontextprotocol.io/specification/2025-11-25/server/tools

## 13. 下一会话开始时的建议步骤

1. 检查 `git status`，不要覆盖用户已有修改。
2. 重新运行第 11.1 节生成代码复现，确认基线未变化。
3. 先增加失败的回归测试，不要先改实现。
4. 明确第 8 节的架构选择，至少确认第一阶段范围。
5. 首个实现目标应是：
   - `EchoOptions.JsonObject` 的 Schema 默认值从 `"1"` 变成正确 wire value。
   - 增加对应生成源码或 Schema 断言。
6. 再处理 `JsonStringEnumMemberName` 和枚举返回值。
7. 多态和任意命名策略应独立提交或至少独立测试，避免一次改动过大。
8. 最后更新三种语言文档。

## 14. 相邻风险：尚未完成审计

下一会话不要把本文当成完整的 `System.Text.Json` Schema conformance 结论。建议后续继续检查：

- `JsonIgnore` 是否被 Schema 正确排除。
- `JsonInclude` 和字段序列化。
- `JsonRequired`。
- C# `required` 与可空性的组合；“必须出现”和“允许 null”不应混为一谈。
- record 主构造函数参数的 required 行为。
- 只读/只写属性。
- `JsonConverter` 应用于类型或属性时的 Schema。
- 字典值并非 `string` 时的 Schema。
- `JsonNumberHandling` 允许字符串数字时与 Schema 的关系。
- `JsonUnmappedMemberHandling` / `additionalProperties`。
- 集合具体类型和可反序列化能力。
- `DateTime`、`Guid`、`Uri` 等字符串格式类型是否需要 `format`。
- 多态基类上的未知派生类型处理设置。
- 输出 Schema 的 required 语义是否与实际序列化必然出现的属性一致。

这些项目目前是**待审计项**，除非下一会话用源码和测试确认，否则不要直接标记为已确认缺陷。

## 15. 工作区注意事项

编写本文时，工作区原本已经有用户修改：

```text
M docs/tasks/01-schema-conformance-baseline/plan.md
```

该修改与本文任务无关，不要回退或覆盖。

本文创建时没有修改生产代码、测试代码或现有简体中文文档。
