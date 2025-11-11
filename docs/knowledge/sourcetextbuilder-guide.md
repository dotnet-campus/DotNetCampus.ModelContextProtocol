# SourceTextBuilder API 使用指南

> 本文档介绍 `DotNetCampus.CodeAnalysisUtils` 库中的 `SourceTextBuilder` API 的使用方法。

## 概述

`SourceTextBuilder` 是专为 Roslyn 源生成器设计的**链式代码生成构建器**，用于生成格式良好、缩进正确的 C# 代码。

**核心特性**：
- **链式调用**：流畅的 API 设计，所有方法返回 builder 自身
- **自动缩进**：自动处理代码缩进和格式化
- **类型安全**：通过接口约束确保 API 调用的上下文正确性
- **高性能**：零中间分配，直接写入 `IndentedStringBuilder`

## 接口层次结构

```
ISourceTextBuilder                  // 基础接口
├─ IAllowNestedSourceTextBuilder  // 嵌套源代码
│  ├─ IAllowScopedNamespace      // 命名空间
│  ├─ IAllowTypeDeclaration      // 类型声明（class/interface/struct 等）
│  ├─ IAllowMemberDeclaration    // 成员声明（方法/属性/字段等）
│  └─ IAllowStatements           // 语句块
├─ IAllowDocumentationComment     // 文档注释
├─ IAllowAttributes               // 特性标注
└─ IAllowTypeConstraints          // 泛型约束
```

## API 速查表

### 初始化与配置

| API                                 | 说明                          | 默认值   |
| ----------------------------------- | ----------------------------- | -------- |
| `new SourceTextBuilder(namespace)`  | 创建构建器                    | -        |
| `UseFileScopedNamespace`            | 使用文件作用域命名空间        | `true`   |
| `SimplifyTypeNamesByUsingNamespace` | 简化类型名（通过 using 语句） | `false`  |
| `ShouldPrependGlobal`               | 是否添加 `global::` 前缀      | `true`   |
| `Indentation`                       | 缩进字符                      | `"    "` |
| `NewLine`                           | 换行符                        | `"\n"`   |

### 引用管理

| API                                    | 说明                |
| -------------------------------------- | ------------------- |
| `Using(string namespace)`              | 添加 using 命名空间 |
| `UsingStatic(string type)`             | 添加 using static   |
| `UsingTypeAlias(string alias, string)` | 添加类型别名        |

### 类型声明

| API                       | 适用接口               | 说明          |
| ------------------------- | ---------------------- | ------------- |
| `AddTypeDeclaration(...)` | IAllowTypeDeclaration  | 添加类型声明  |
| `AddBaseTypes(...)`       | TypeDeclarationBuilder | 添加基类/接口 |

### 成员声明

| API                         | 适用接口                | 说明           |
| --------------------------- | ----------------------- | -------------- |
| `AddMethodDeclaration(...)` | IAllowMemberDeclaration | 添加方法       |
| `AddRawText(string)`        | 所有 Builder            | 添加原始文本行 |
| `AddRawMembers(...)`        | IAllowMemberDeclaration | 批量添加成员   |

### 语句生成

| API                       | 适用接口         | 说明           |
| ------------------------- | ---------------- | -------------- |
| `AddRawStatement(string)` | IAllowStatements | 添加单条语句   |
| `AddRawStatements(...)`   | IAllowStatements | 批量添加语句   |
| `AddBracketScope(...)`    | IAllowStatements | 添加括号作用域 |

### 条件生成

| API                       | 说明                     |
| ------------------------- | ------------------------ |
| `Condition(bool, Action)` | 条件分支（源生成时判断） |
| `Otherwise(Action)`       | else 分支                |
| `EndCondition()`          | 结束条件链（可选）       |

### 文档注释

| API                                | 说明                 |
| ---------------------------------- | -------------------- |
| `WithSummaryComment(string)`       | 添加 summary 注释    |
| `WithRawDocumentationComment(...)` | 添加原始文档注释     |
| `DocumentationCommentBuilder`      | 完整的文档注释构建器 |

## 快速开始

### 基本用法

```csharp
using var builder = new SourceTextBuilder("MyNamespace")
{
    UseFileScopedNamespace = true,
    ShouldPrependGlobal = true
};

builder
    .Using("System")
    .Using("System.Text.Json")
    .AddTypeDeclaration("public sealed class MyClass", t => t
        .WithSummaryComment("这是一个示例类")
        .AddBaseTypes("IDisposable")
        .AddRawText("private int _value;")
    );

string code = builder.ToString();
```

### 添加方法

```csharp
t.AddMethodDeclaration("public int Calculate(int x, int y)", m => m
    .WithSummaryComment("计算两个数的和")
    .AddRawStatements(
        "var result = x + y;",
        "return result;"
    )
);
```

### 批量添加语句

```csharp
// 使用 params 参数
m.AddRawStatements(
    "var x = 1;",
    "var y = 2;",
    "return x + y;"
);

// 使用 IEnumerable
m.AddRawStatements(parameters.Select(p => $"var {p.Name} = default;"));

// 使用 LINQ 链式转换
m.AddRawStatements(model.Parameters
    .Select(p => (Name: p.Name, Type: p.Type, JsonName: ToKebabCase(p.Name)))
    .Select(p => $"var {p.Name} = ParseJson(\"{p.JsonName}\", typeof({p.Type}));")
);
```

### 条件分支生成

```csharp
builder
    .Condition(isAsync, async => async
        .AddRawStatements(
            $"var result = await {methodCall}.ConfigureAwait(false);",
            $"return result;"
        ))
    .Otherwise(sync => sync
        .AddRawStatements(
            $"var result = {methodCall};",
            $"return ValueTask.FromResult(result);"
        ));
```

**重要**：`Condition` 在**源生成时**执行判断，只有一个分支会被生成到最终代码中。

### 添加括号作用域

```csharp
m.AddBracketScope("return new()", "{", "};", b => b
    .AddRawText("Name = \"value1\",")
    .AddRawText("Title = \"value2\",")
);

// 生成：
// return new()
// {
//     Name = "value1",
//     Title = "value2",
// };
```

### 文档注释

```csharp
// 简单注释
t.WithSummaryComment("类的说明");

// 继承文档
m.WithRawDocumentationComment("/// <inheritdoc />");

// 完整注释
builder.DocumentationCommentBuilder
    .Summary("方法说明")
    .AddParam("param1", "参数1说明")
    .AddParam("param2", "参数2说明")
    .Returns("返回值说明")
    .Remarks("备注信息");
```

## 最佳实践

### 1. 使用 using 语句管理生命周期

```csharp
using var builder = new SourceTextBuilder("MyNamespace");
// builder 会在作用域结束时自动释放
```

### 2. 优先使用链式调用

```csharp
// ✅ 推荐：完整链式调用
builder
    .Using("System")
    .AddTypeDeclaration("public class MyClass", t => t
        .AddMethodDeclaration("public void Method()", m => m
            .AddRawStatement("Console.WriteLine();")
        )
    );
```

### 3. 使用批量 API 而非循环

```csharp
// ❌ 不推荐
foreach (var param in parameters)
{
    m.AddRawStatement($"var {param} = default;");
}

// ✅ 推荐
m.AddRawStatements(parameters.Select(p => $"var {p} = default;"));
```

### 4. 使用 LINQ 进行数据转换

```csharp
// ✅ 使用元组辅助数据转换
m.AddRawStatements(model.Parameters
    .Select(p => (
        Name: p.Name,
        Type: p.Type,
        JsonName: NamingHelper.MakeKebabCase(p.Name),
        HasDefault: p.HasExplicitDefaultValue
    ))
    .Select(p => GenerateParameterCode(p))
);
```

### 5. 使用插值字符串而非拼接

```csharp
// ❌ 不推荐：字符串拼接
var code = "var x = ";
code += value.ToString();
code += ";";
m.AddRawStatement(code);

// ✅ 推荐：插值字符串
m.AddRawStatement($"var x = {value};");

// ✅ 推荐：多行字符串（C# 11+）
var signature = $"""
    public async ValueTask<Result> Method(
        {param1},
        {param2})
    """;
```

### 6. 封装复杂逻辑为扩展方法

```csharp
file static class Extensions
{
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
}
```

### 7. 使用 Condition/Otherwise 替代 if-else

```csharp
// ❌ 破坏链式调用
if (condition)
{
    builder.AddRawStatement("...");
}
else
{
    builder.AddRawStatement("...");
}

// ✅ 保持链式调用
builder
    .Condition(condition, x => x.AddRawStatement("..."))
    .Otherwise(x => x.AddRawStatement("..."));
```

## 性能优化

### 避免重复字符串分配

```csharp
// ✅ 流式序列化，零中间分配
builder.ToString();  // 直接从 IndentedStringBuilder 获取
```

### 批量操作优于单次操作

```csharp
// ✅ 一次调用处理多个语句
m.AddRawStatements(
    "statement1;",
    "statement2;",
    "statement3;"
);

// ✅ 使用 IEnumerable 延迟计算
m.AddRawStatements(items
    .Where(predicate)
    .Select(transform)
);
```

## 扩展开发

### 创建自定义扩展方法

扩展方法应遵循以下原则：
1. 返回接口类型（如 `IAllowMemberDeclaration`）以保持链式调用
2. 使用 `file static class` 限制作用域
3. 参数化配置，提高复用性

```csharp
file static class Extensions
{
    /// <summary>
    /// 为类型添加标准的工具方法
    /// </summary>
    public static IAllowMemberDeclaration AddToolMethod(
        this IAllowMemberDeclaration builder, 
        ToolModel model)
    {
        return builder.AddMethodDeclaration(
            $"public ValueTask<Result> {model.Name}(...)", 
            m => m
                .WithRawDocumentationComment("/// <inheritdoc />")
                .AddRawStatements(GenerateMethodBody(model))
        );
    }

    private static IEnumerable<string> GenerateMethodBody(ToolModel model)
    {
        // 生成方法体逻辑
        yield return "// 实现代码";
    }
}
```

## 完整示例

### 生成类型定义

```csharp
private string GenerateBridgeClass(Model model)
{
    var targetFactory = $"global::System.Func<{model.ContainingType}>";

    using var builder = new SourceTextBuilder(model.Namespace)
        .Using("System")
        .Using("System.Text.Json")
        .AddTypeDeclaration($"public sealed class {model.BridgeTypeName}({targetFactory} targetFactory)", t => t
            .AddBaseTypes("IBridge")
            .WithSummaryComment($"为 <see cref=\"{model.ContainingType}.{model.MethodName}\"/> 生成的桥接类")
            .AddRawMembers(
                $"private readonly {targetFactory} _targetFactory = targetFactory;",
                $"private {model.ContainingType} Target => _targetFactory();"
            )
            .AddRawText($"public string Name {{ get; }} = \"{model.Name}\";")
            .AddCallMethod(model)
        );

    return builder.ToString();
}

private static IAllowMemberDeclaration AddCallMethod(
    this IAllowMemberDeclaration builder,
    Model model)
{
    var methodCall = $"Target.{model.MethodName}({BuildArguments(model)})";

    return builder.AddMethodDeclaration("public ValueTask<Result> CallAsync(...)", m => m
        .WithRawDocumentationComment("/// <inheritdoc />")
        .AddRawStatements(GenerateParameterParsing(model))
        .Condition(model.IsAsync, async => async
            .AddRawStatements(
                $"var result = await {methodCall}.ConfigureAwait(false);",
                "return (Result)result;"
            ))
        .Otherwise(sync => sync
            .AddRawStatements(
                $"var result = {methodCall};",
                "return ValueTask.FromResult((Result)result);"
            ))
    );
}

private static IEnumerable<string> GenerateParameterParsing(Model model)
{
    return model.Parameters
        .Select(p => (
            Name: p.Name,
            Type: p.Type,
            JsonName: ToKebabCase(p.Name),
            HasDefault: p.HasExplicitDefaultValue
        ))
        .Select(p => $"""
            var {p.Name} = jsonArgs.TryGetProperty("{p.JsonName}", out var prop)
                ? prop.Deserialize<{p.Type}>()
                : {(p.HasDefault ? FormatDefault(p) : $"throw new ArgumentException(\"{p.JsonName}\")")};
            """);
}
```

## 相关资源

- [DotNetCampus.CodeAnalysisUtils GitHub](https://github.com/dotnet-campus/DotNetCampus.CodeAnalysisUtils)
- [项目开发指南](../.github/copilot-instructions.md)
- [McpServerToolGenerator 实现](../../src/DotNetCampus.ModelContextProtocol.Analyzer/Generators/McpServerToolGenerator.cs)
