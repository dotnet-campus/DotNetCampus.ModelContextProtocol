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
