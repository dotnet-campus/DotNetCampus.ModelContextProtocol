# SourceTextBuilder API 使用指南

> 本文档介绍 `DotNetCampus.CodeAnalysisUtils` 库中的 `SourceTextBuilder` API 的使用方法。

## 📌 核心概念

`SourceTextBuilder` 是一个用于**链式生成源代码文本**的构建器，专为 Roslyn 源生成器设计。它提供了流畅的 API 来生成格式良好、缩进正确的 C# 代码。

### 设计理念

- ✅ **链式调用**：所有方法返回 builder 自身，支持流畅的链式调用
- ✅ **自动缩进**：自动处理代码缩进，无需手动管理
- ✅ **类型安全**：通过接口约束确保只能在合适的上下文中调用特定方法
- ✅ **零分配**：直接写入 `IndentedStringBuilder`，避免中间字符串分配

## 🔧 核心接口层次

```
ISourceTextBuilder                  // 所有构建器的基础接口
├─ IAllowNestedSourceTextBuilder   // 支持嵌套源代码
│  ├─ IAllowScopedNamespace        // 允许添加命名空间
│  ├─ IAllowTypeDeclaration        // 允许添加类型声明
│  ├─ IAllowMemberDeclaration      // 允许添加成员声明
│  └─ IAllowStatements             // 允许添加语句
├─ IAllowDocumentationComment      // 允许添加文档注释
├─ IAllowAttributes                // 允许添加特性
└─ IAllowTypeConstraints           // 允许添加泛型约束
```

## 🚀 基本用法

### 1. 创建 SourceTextBuilder

```csharp
using var builder = new SourceTextBuilder("MyNamespace")
{
    UseFileScopedNamespace = true,     // 使用文件作用域命名空间（默认 true）
    SimplifyTypeNamesByUsingNamespace = false,  // 是否简化类型名（默认 false）
    ShouldPrependGlobal = true,        // 是否添加 global:: 前缀（默认 true）
    Indentation = "    ",              // 缩进字符（默认 4 空格）
    NewLine = "\n"                     // 换行符（默认 \n）
};

// 生成代码
string code = builder.ToString();
```

### 2. 添加 Using 引用

```csharp
builder
    .Using("System")
    .Using("System.Text.Json")
    .UsingStatic("System.Math")
    .UsingTypeAlias("JObject", "System.Text.Json.JsonObject");
```

### 3. 添加类型声明

```csharp
builder.AddTypeDeclaration("public sealed class MyClass", t => t
    .WithSummaryComment("这是一个示例类")
    .AddBaseTypes("IDisposable", "IEquatable<MyClass>")
    .AddRawText("private int _value;")
);
```

## 📝 常用 API 详解

### 类型声明相关

#### AddTypeDeclaration
```csharp
builder.AddTypeDeclaration("public class MyClass", t => t
    .WithSummaryComment("类的说明")
    .AddBaseTypes("BaseClass", "IInterface")
    .AddRawText("private int _field;")
    .AddMethodDeclaration("public void MyMethod()", m => m
        .WithRawDocumentationComment("/// <inheritdoc />")
        .AddRawStatement("Console.WriteLine(\"Hello\");")
    )
);
```

### 方法声明相关

#### AddMethodDeclaration
```csharp
t.AddMethodDeclaration("public int Calculate(int x, int y)", m => m
    .WithSummaryComment("计算两个数的和")
    .AddRawStatements(
        "var result = x + y;",
        "return result;"
    )
);
```

#### AddRawStatements (批量添加)
```csharp
// 方式 1: 传入多个字符串参数
m.AddRawStatements(
    "var x = 1;",
    "var y = 2;",
    "return x + y;"
);

// 方式 2: 传入 IEnumerable<string>
m.AddRawStatements(parameters.Select(p => $"var {p.Name} = default;"));

// 方式 3: 使用 LINQ 链式生成
m.AddRawStatements(model.Parameters
    .Select(p => (Name: p.Name, Type: p.Type, JsonName: ToKebabCase(p.Name)))
    .Select(p => $"var {p.Name} = ParseJson(\"{p.JsonName}\", typeof({p.Type}));")
);
```

### 代码块作用域

#### AddBracketScope
```csharp
m.AddBracketScope("return new()", "{", "};", b => b
    .AddPropertyAssignment("Name", "value1")
    .AddPropertyAssignment("Title", "value2")
);

// 生成：
// return new()
// {
//     Name = "value1",
//     Title = "value2",
// };
```

### 控制流（条件分支）

#### Condition / Otherwise（链式条件判断）
```csharp
builder
    .Condition(condition, x => x
        // 异步分支：await 并转换
        .AddRawStatements(
            $"var result = await {methodCall}.ConfigureAwait(false);",
            $"return ({callToolResult})result;"
        ))
    .Otherwise(x => x
        // 同步分支：直接转换并返回
        .AddRawStatements(
            $"var result = {methodCall};",
            $"return {valueTask}.FromResult(({callToolResult})result);"
        ));

// 生成代码根据条件选择分支（只有一个分支会被生成）
```

**API 签名**：
```csharp
// 开始条件链
builder
    .Condition(bool condition, Action<TBuilder> action)
    .Otherwise(Action<TBuilder> action)
    .EndCondition();  // 可选：显式结束条件链
```

**注意事项**：
- `Condition` 和 `Otherwise` 在**源生成时**判断，而不是在运行时
- 只有一个分支会被生成到最终代码中
- 可以链式调用多个 `Condition`，类似于 if-else if-else

### 文档注释

#### WithSummaryComment
```csharp
t.WithSummaryComment("这是一个类的说明");
// 生成: /// <summary>这是一个类的说明</summary>
```

#### WithRawDocumentationComment
```csharp
m.WithRawDocumentationComment("/// <inheritdoc />");
// 可以带或不带 /// 前缀，都会正确处理
```

#### 完整的文档注释
```csharp
// 通过 DocumentationCommentBuilder 添加复杂注释
builder.DocumentationCommentBuilder
    .Summary("方法说明")
    .AddParam("param1", "参数1说明")
    .AddParam("param2", "参数2说明")
    .Returns("返回值说明")
    .Remarks("备注信息")
    .AddRawText("/// <example>示例代码</example>");
```

### 添加原始文本

#### AddRawText (单行)
```csharp
t.AddRawText("private int _value;");
t.AddRawText("public string Name { get; set; }");
```

#### AddRawMembers (批量成员)
```csharp
t.AddRawMembers(
    "private int _x;",
    "private int _y;",
    "public int Sum => _x + _y;"
);
```

## 🎯 实际应用示例

### 示例 1: 生成 MCP 服务器工具桥接类

```csharp
private string GenerateMcpServerToolBridgeCode(McpServerToolGeneratingModel model)
{
    var targetFactory = $"global::System.Func<{model.ContainingType.ToUsingString()}>";
    
    var builder = new SourceTextBuilder(model.Namespace)
        .Using("System.Text.Json")
        .AddTypeDeclaration($"public sealed class {model.GetBridgeTypeName()}({targetFactory} targetFactory)", t => t
            .AddBaseTypes("global::DotNetCampus.ModelContextProtocol.CompilerServices.IGeneratedMcpServerToolBridge")
            .WithSummaryComment($"为 <see cref=\"{model.ContainingType.ToUsingString()}.{model.Method.Name}\"/> 方法生成的 MCP 服务器工具桥接类。")
            .AddRawText($"private readonly {targetFactory} _targetFactory = targetFactory;")
            .AddRawText($"private {model.ContainingType.ToUsingString()} Target => _targetFactory();")
            .AddRawText($"public string ToolName {{ get; }} = \"{model.Name}\";")
            .AddGetToolDefinitionMethod(model)
            .AddCallToolMethod(model)
        );
    
    return builder.ToString();
}
```

### 示例 2: 生成方法体（链式 LINQ）

```csharp
m.AddRawStatements(model.Parameters
    .Select(p => (
        Parameter: p,
        Name: p.Name,
        Type: p.Type,
        JsonName: NamingHelper.MakeKebabCase(p.Name, true, true),
        HasDefault: p.HasExplicitDefaultValue
    ))
    .Select(p => $"""
        var {p.Name} = jsonArguments.TryGetProperty("{p.JsonName}", out var {p.Name}Property)
            ? {p.Name}Property.Deserialize((JsonTypeInfo<{p.Type.ToUsingString()}>)context.GetTypeInfo(typeof({p.Type}))!)
            : {(p.HasDefault ? FormatDefaultValue(p.Parameter) : $"throw new MissingRequiredArgumentException(\"{p.JsonName}\")")};
        """
    )
);
```

## ⚡ 性能优化建议

### 1. 使用批量 API
```csharp
// ❌ 不推荐：多次调用
foreach (var param in parameters)
{
    m.AddRawStatement($"var {param} = default;");
}

// ✅ 推荐：批量调用
m.AddRawStatements(parameters.Select(p => $"var {p} = default;"));
```

### 2. 使用 LINQ 链式生成
```csharp
// ✅ 优雅且高效
m.AddRawStatements(model.Parameters
    .Where(p => !p.HasExplicitDefaultValue)
    .Select(p => GenerateParameterCode(p))
);
```

### 3. 避免中间字符串拼接
```csharp
// ❌ 不推荐：多次字符串拼接
var code = "var x = ";
code += value.ToString();
code += ";";
m.AddRawStatement(code);

// ✅ 推荐：使用插值字符串
m.AddRawStatement($"var x = {value};");
```

## 🔍 扩展方法编写指南

### 创建自定义扩展方法

```csharp
file static class Extensions
{
    // 为 IAllowMemberDeclaration 添加扩展
    public static IAllowMemberDeclaration AddPropertyAssignment(
        this ISourceTextBuilder builder, 
        string propertyName, 
        string? stringValue)
    {
        builder.AddRawText($"{propertyName} = {stringValue switch {
            null => "null",
            _ => $"\"{stringValue}\"",
        }},");
        return builder;
    }
    
    // 为特定模型添加扩展
    public static IAllowMemberDeclaration AddCallToolMethod(
        this IAllowMemberDeclaration builder, 
        McpServerToolGeneratingModel model)
    {
        return builder.AddMethodDeclaration("public ValueTask<CallToolResult> CallTool(...)", m => m
            .WithRawDocumentationComment("/// <inheritdoc />")
            .AddRawStatements(GenerateStatements(model))
        );
    }
}
```

## 📚 API 速查表

| API                                   | 适用接口                         | 说明                |
| ------------------------------------- | -------------------------------- | ------------------- |
| `Using(string)`                       | SourceTextBuilder                | 添加 using 命名空间 |
| `UsingStatic(string)`                 | SourceTextBuilder                | 添加 using static   |
| `AddTypeDeclaration(...)`             | IAllowTypeDeclaration            | 添加类型声明        |
| `AddMethodDeclaration(...)`           | IAllowMemberDeclaration          | 添加方法声明        |
| `AddRawText(string)`                  | 所有 Builder                     | 添加单行原始文本    |
| `AddRawStatements(...)`               | IAllowStatements                 | 批量添加语句        |
| `AddRawMembers(...)`                  | IAllowMemberDeclaration          | 批量添加成员        |
| `AddBracketScope(...)`                | IAllowStatements                 | 添加带括号的作用域  |
| `WithSummaryComment(string)`          | IAllowDocumentationComment       | 添加 summary 注释   |
| `WithRawDocumentationComment(string)` | IAllowDocumentationComment       | 添加原始文档注释    |
| `AddBaseTypes(...)`                   | TypeDeclarationSourceTextBuilder | 添加基类/接口       |

## 🎓 最佳实践

1. **始终使用 `using` 语句**
   ```csharp
   using var builder = new SourceTextBuilder("MyNamespace");
   ```

2. **优先使用链式调用**
   ```csharp
   builder
       .Using("System")
       .AddTypeDeclaration("public class MyClass", t => t
           .AddMethodDeclaration("public void Method()", m => m
               .AddRawStatement("Console.WriteLine();")
           )
       );
   ```

3. **使用 LINQ 生成重复代码**
   ```csharp
   m.AddRawStatements(items.Select(item => $"Process({item});"));
   ```

4. **善用扩展方法封装复杂逻辑**
   ```csharp
   file static class MyExtensions
   {
       public static IAllowMemberDeclaration AddMyCustomMethod(
           this IAllowMemberDeclaration builder, Model model) => ...
   }
   ```

## 🔗 相关资源

- [DotNetCampus.CodeAnalysisUtils GitHub](https://github.com/dotnet-campus/DotNetCampus.CodeAnalysisUtils)
- [项目开发指南](../.github/copilot-instructions.md)
- [McpServerToolGenerator 实现](../../src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/McpServerToolGenerator.cs)

---

## 📋 附录：实战优化案例总结

### 案例：处理异步方法和 CancellationToken

**问题场景**：
1. 接口方法需要添加 `CancellationToken` 参数
2. 目标方法可能是异步的（返回 `Task`/`ValueTask`）
3. 返回值需要隐式转换为 `CallToolResult`

**优化前的代码**（复杂且难以维护）：
```csharp
// ❌ 使用 if 语句，破坏链式调用
if (model.GetIsAsync())
{
    m.AddRawStatements(
        $"var result = await {methodCall}.ConfigureAwait(false);",
        $"return ({callToolResult})result;"
    );
}
else
{
    m.AddRawStatements(
        $"var result = {methodCall};",
        $"return {valueTask}.FromResult(({callToolResult})result);"
    );
}
```

**优化后的代码**（使用 Condition/Otherwise）：
```csharp
// ✅ 完全链式调用，清晰简洁
builder
    .Condition(model.GetIsAsync(), async => async
        .AddRawStatements(
            $"var result = await {methodCall}.ConfigureAwait(false);",
            $"return ({callToolResult})result;"
        ))
    .Otherwise(sync => sync
        .AddRawStatements(
            $"var result = {methodCall};",
            $"return {valueTask}.FromResult(({callToolResult})result);"
        ));
```

### 核心优化原则

1. **源生成器代码优先可读性**
   - 源生成器代码需要被其他开发者阅读和维护
   - 链式调用提高可读性，减少缩进层次

2. **生成的代码优先性能**
   - 使用 `ConfigureAwait(false)` 避免上下文切换
   - 使用 `ValueTask` 减少异步分配
   - 直接类型转换而非装箱

3. **优先使用链式调用**
   - 使用 `Condition/Otherwise` 代替 if-else
   - 使用 `AddRawStatements(IEnumerable)` 代替循环
   - 使用 LINQ `Select` 进行数据转换

4. **难以链式化时抽取扩展方法**
   - 将复杂逻辑封装为扩展方法
   - 保持主流程的链式调用风格
   - 使用 `file static class` 限制作用域

### 关键技巧

#### 1. 过滤特殊参数
```csharp
// 过滤掉 CancellationToken，因为从外部传入
var methodParameters = model.Method.Parameters
    .Where(p => !IsCancellationTokenParameter(p))
    .ToArray();
```

#### 2. 元组辅助数据转换
```csharp
// 使用元组将多个属性组合，便于后续 Select
.Select(p => (Parameter: p, Name: p.Name, Type: p.Type,
    JsonName: NamingHelper.MakeKebabCase(p.Name, true, true), 
    HasDefault: p.HasExplicitDefaultValue))
.Select(p => $"var {p.Name} = ...{p.JsonName}...{p.HasDefault}...")
```

#### 3. 多行字符串模板
```csharp
// 使用原始字符串字面量（C# 11+）保持格式
var signature = $"""
    public {(model.GetIsAsync() ? "async " : "")}{valueTask}<{callToolResult}> CallTool(
        {jsonElement} jsonArguments,
        {jsonSerializerContext} jsonSerializerContext,
        {cancellationToken} cancellationToken)
    """;
```

#### 4. 条件表达式
```csharp
// 在字符串插值中使用三元表达式
$"public {(model.GetIsAsync() ? "async " : "")}{valueTask}<{callToolResult}> CallTool(...)"

// 在 Select 中使用三元表达式处理默认值
: {(p.HasDefault ? FormatDefaultValue(p.Parameter) : $"throw new {exception}(\"{p.JsonName}\")")};
```

### 完整示例

参见 [`McpServerToolGenerator.cs`](../../src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/McpServerToolGenerator.cs) 中的完整实现。
