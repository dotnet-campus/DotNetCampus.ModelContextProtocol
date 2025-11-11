using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators;

[Generator(LanguageNames.CSharp)]
public class McpServerToolGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var toolMethodProvider = context.SyntaxProvider.ForAttributeWithMetadataName(typeof(McpServerToolAttribute).FullName!,
                (n, ct) => n is MethodDeclarationSyntax, (c, ct) =>
                {
                    if (c.SemanticModel.GetDeclaredSymbol(c.TargetNode, ct) is not IMethodSymbol methodSymbol)
                    {
                        return null;
                    }

                    return McpServerToolGeneratingModel.TryParse(methodSymbol, ct);
                })
            .Where(m => m is not null)
            .Select((m, ct) => m!);

        context.RegisterSourceOutput(toolMethodProvider, Execute);
    }

    private void Execute(SourceProductionContext context, McpServerToolGeneratingModel model)
    {
        var code = GenerateMcpServerToolBridgeCode(model);
        context.AddSource($"ModelContextProtocol.Bridges/{model.ContainingType.ToDisplayString()}.{model.Method.Name}.cs", code);
    }

    private string GenerateMcpServerToolBridgeCode(McpServerToolGeneratingModel model)
    {
        var targetFactory = $"{G.Func}<{model.ContainingType.ToUsingString()}>";

        using var builder = new SourceTextBuilder(model.Namespace)
            .Using("System.Text.Json")
            .AddTypeDeclaration($"{model.GetGetAccessModifier()} sealed class {model.GetBridgeTypeName()}({targetFactory} targetFactory)", t => t
                .AddBaseTypes(G.IMcpServerTool)
                .WithSummaryComment($"为 <see cref=\"{model.ContainingType.ToUsingString()}.{model.Method.Name}\"/> 方法生成的 MCP 服务器工具桥接类。")
                .AddRawMembers(
                    $"private readonly {targetFactory} _targetFactory = targetFactory;",
                    $"private {model.ContainingType.ToUsingString()} Target => _targetFactory();",
                    $"/// <inheritdoc />\npublic string ToolName {{ get; }} = \"{model.Name}\";"
                )
                .AddGetToolDefinitionMethod(model)
                .AddCallToolMethod(model)
            );

        return builder.ToString();
    }
}

file static class Extensions
{
    /// <summary>
    /// 为 MCP 工具桥接类添加 GetToolDefinition 方法。
    /// </summary>
    public static IAllowMemberDeclaration AddGetToolDefinitionMethod(
        this IAllowMemberDeclaration builder, McpServerToolGeneratingModel model)
    {
        return builder
            .AddMethodDeclaration(true,
                $"public {G.Tool} GetToolDefinition({G.InputSchemaJsonObjectJsonContext} jsonContext)",
                m => m
                    .WithRawDocumentationComment("/// <inheritdoc />")
                    .AddBracketScope("new()", "{", "}", b => b
                        .AddStringAssignment("Name", model.Name)
                        .AddStringAssignment("Title", model.Title)
                        .AddStringAssignment("Description", model.Description)
                        .AddRawText(
                            $"InputSchema = {G.JsonSerializer}.SerializeToElement(GetInputSchema(jsonContext), jsonContext.InputSchemaJsonObject),")
                    )
            )
            .AddMethodDeclaration($"private {G.InputSchemaJsonObject} GetInputSchema({G.InputSchemaJsonObjectJsonContext} jsonContext)",
                m => m
                    .AddNewInputSchema(model)
            );
    }

    private static IAllowStatement AddNewInputSchema(this IAllowStatement builder, McpServerToolGeneratingModel model)
    {
        // 将参数转换为统一的 schema 成员描述
        var members = model.Parameters
            .Select(p => new SchemaMemberDescriptor(
                p.JsonName,
                p.Type,
                p.GetJsonEscapedDescription(),
                p.IsRequired
            ))
            .ToArray();

        return builder.AddRawStatement($"return {GenerateSchemaExpression(members, null)};");
    }

    /// <summary>
    /// 生成 schema 表达式（递归）。
    /// </summary>
    /// <param name="members">成员描述列表（参数或属性）</param>
    /// <param name="description">当前层的描述</param>
    private static string GenerateSchemaExpression(SchemaMemberDescriptor[] members, string? description)
    {
        var requiredMembers = members
            .Where(m => m.IsRequired)
            .Select(m => $"\"{m.JsonName}\"")
            .ToArray();

        var requiredExpr = requiredMembers.Length > 0
            ? $"new[] {{ {string.Join(", ", requiredMembers)} }}"
            : "null";

        var descriptionExpr = description != null ? $"\"{description}\"" : "null";

        var propertiesExpr = members.Length > 0
            ? GeneratePropertiesDictionary(members)
            : "null";

        return $$"""
            new {{G.InputSchemaJsonObject}}
            {
                RawType = JsonSerializer.SerializeToElement("object", jsonContext.String),
                Default = null,
                Description = {{descriptionExpr}},
                Enum = null,
                Items = null,
                Required = {{requiredExpr}},
                Properties = {{propertiesExpr}},
            }
            """;
    }

    /// <summary>
    /// 生成 Properties 字典表达式。
    /// </summary>
    private static string GeneratePropertiesDictionary(SchemaMemberDescriptor[] members)
    {
        var entries = members
            .Select(m => $"{{ \"{m.JsonName}\", {GenerateMemberSchemaExpression(m)} }}")
            .ToArray();

        return $$"""
            new Dictionary<string, {{G.InputSchemaJsonObject}}>
            {
                {{string.Join(",\n    ", entries)}},
            }
            """;
    }

    /// <summary>
    /// 生成单个成员的 schema 表达式（递归处理嵌套对象）。
    /// </summary>
    private static string GenerateMemberSchemaExpression(SchemaMemberDescriptor member)
    {
        // 处理可空类型
        var (coreType, isNullable) = UnwrapNullableType(member.Type);

        // 获取 JSON Schema 类型
        var jsonSchemaType = GetJsonSchemaType(coreType);

        // 生成 RawType 表达式
        var rawTypeExpr = isNullable
            ? $"JsonSerializer.SerializeToElement(new[] {{ \"{jsonSchemaType}\", \"null\" }}, jsonContext.IReadOnlyListString)"
            : $"JsonSerializer.SerializeToElement(\"{jsonSchemaType}\", jsonContext.String)";

        var descriptionExpr = member.Description != null ? $"\"{member.Description}\"" : "null";

        // 处理数组类型的 Items
        if (jsonSchemaType == "array")
        {
            var elementType = GetArrayElementType(coreType);
            if (elementType != null)
            {
                var elementDescriptor = new SchemaMemberDescriptor(
                    member.JsonName,
                    elementType,
                    null,
                    true
                );
                var itemsExpr = GenerateMemberSchemaExpression(elementDescriptor);

                return $$"""
                    new {{G.InputSchemaJsonObject}}
                    {
                        RawType = {{rawTypeExpr}},
                        Default = null,
                        Description = {{descriptionExpr}},
                        Enum = null,
                        Items = {{itemsExpr}},
                        Required = null,
                        Properties = null,
                    }
                    """;
            }
        }

        // 处理对象类型（需要递归生成属性）
        if (jsonSchemaType == "object" && coreType is INamedTypeSymbol namedType)
        {
            var properties = GetObjectProperties(namedType);
            if (properties.Length > 0)
            {
                var propertiesExpr = GeneratePropertiesDictionary(properties);

                // 提取所有必需属性
                var requiredProps = properties
                    .Where(p => p.IsRequired)
                    .Select(p => $"\"{p.JsonName}\"")
                    .ToArray();

                var requiredExpr = requiredProps.Length > 0
                    ? $"new[] {{ {string.Join(", ", requiredProps)} }}"
                    : "null";

                return $$"""
                    new {{G.InputSchemaJsonObject}}
                    {
                        RawType = {{rawTypeExpr}},
                        Default = null,
                        Description = {{descriptionExpr}},
                        Enum = null,
                        Items = null,
                        Required = {{requiredExpr}},
                        Properties = {{propertiesExpr}},
                    }
                    """;
            }
        }

        // 简单类型（无 Items 和 Properties）
        return $$"""
            new {{G.InputSchemaJsonObject}}
            {
                RawType = {{rawTypeExpr}},
                Default = null,
                Description = {{descriptionExpr}},
                Enum = null,
                Items = null,
                Required = null,
                Properties = null,
            }
            """;
    }

    /// <summary>
    /// 获取对象类型的所有属性。
    /// </summary>
    private static SchemaMemberDescriptor[] GetObjectProperties(INamedTypeSymbol typeSymbol)
    {
        var properties = new List<SchemaMemberDescriptor>();

        // 获取所有公共属性和可访问属性
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IPropertySymbol property &&
                property.DeclaredAccessibility == Accessibility.Public &&
                !property.IsStatic)
            {
                var jsonName = NamingHelper.MakeKebabCase(property.Name, true, true);
                var description = GetPropertyDescription(property);
                var isRequired = !IsNullableType(property.Type);

                properties.Add(new SchemaMemberDescriptor(
                    jsonName,
                    property.Type,
                    description,
                    isRequired
                ));
            }
        }

        // 处理主构造函数参数（record 类型）
        if (typeSymbol.IsRecord)
        {
            foreach (var ctor in typeSymbol.Constructors)
            {
                if (ctor.DeclaredAccessibility == Accessibility.Public)
                {
                    foreach (var param in ctor.Parameters)
                    {
                        var jsonName = NamingHelper.MakeKebabCase(param.Name, true, true);

                        // 检查是否已经通过属性添加
                        if (!properties.Any(p => p.JsonName == jsonName))
                        {
                            var description = GetParameterDescription(param);
                            var isRequired = !IsNullableType(param.Type) && !param.HasExplicitDefaultValue;

                            properties.Add(new SchemaMemberDescriptor(
                                jsonName,
                                param.Type,
                                description,
                                isRequired
                            ));
                        }
                    }
                    break; // 只处理第一个公共构造函数
                }
            }
        }

        return properties.ToArray();
    }

    /// <summary>
    /// 获取属性的描述（从 XML 文档注释）。
    /// </summary>
    private static string? GetPropertyDescription(IPropertySymbol property)
    {
        var xmlComment = property.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xmlComment))
        {
            return null;
        }

        // 简单提取 <summary> 标签内容
        var summaryStart = xmlComment!.IndexOf("<summary>");
        var summaryEnd = xmlComment.IndexOf("</summary>");
        if (summaryStart >= 0 && summaryEnd > summaryStart)
        {
            var summary = xmlComment.Substring(summaryStart + 9, summaryEnd - summaryStart - 9).Trim();
            return EscapeForJsonString(summary);
        }

        return null;
    }

    /// <summary>
    /// 获取参数的描述（从 XML 文档注释）。
    /// </summary>
    private static string? GetParameterDescription(IParameterSymbol parameter)
    {
        // 从包含方法/构造函数获取 XML 注释
        var containingSymbol = parameter.ContainingSymbol;
        var xmlComment = containingSymbol?.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xmlComment))
        {
            return null;
        }

        // 查找对应的 <param name="xxx"> 标签
        var paramTag = $"<param name=\"{parameter.Name}\">";
        var paramStart = xmlComment!.IndexOf(paramTag);
        if (paramStart >= 0)
        {
            var contentStart = paramStart + paramTag.Length;
            var paramEnd = xmlComment.IndexOf("</param>", contentStart);
            if (paramEnd > contentStart)
            {
                var description = xmlComment.Substring(contentStart, paramEnd - contentStart).Trim();
                return EscapeForJsonString(description);
            }
        }

        return null;
    }

    /// <summary>
    /// 转义 JSON 字符串中的特殊字符。
    /// </summary>
    private static string EscapeForJsonString(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// 判断类型是否可空。
    /// </summary>
    private static bool IsNullableType(ITypeSymbol typeSymbol)
    {
        // 值类型的可空形式
        if (typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            return true;
        }

        // 引用类型的可空注解
        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取数组或集合的元素类型。
    /// </summary>
    private static ITypeSymbol? GetArrayElementType(ITypeSymbol typeSymbol)
    {
        // 处理数组类型
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType;
        }

        // 处理泛型集合类型
        if (typeSymbol is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
        {
            var fullName = namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fullName.StartsWith("global::System.Collections.Generic.IEnumerable<") ||
                fullName.StartsWith("global::System.Collections.Generic.List<") ||
                fullName.StartsWith("global::System.Collections.Generic.IList<") ||
                fullName.StartsWith("global::System.Collections.Generic.IReadOnlyList<") ||
                fullName.StartsWith("global::System.Collections.Generic.ICollection<"))
            {
                return namedType.TypeArguments[0];
            }
        }

        return null;
    }

    /// <summary>
    /// 解包可空类型，返回核心类型和是否可空。
    /// </summary>
    private static (ITypeSymbol CoreType, bool IsNullable) UnwrapNullableType(ITypeSymbol typeSymbol)
    {
        // 处理 Nullable<T> (值类型可空)
        if (typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } namedType)
        {
            return (namedType.TypeArguments[0], true);
        }

        // 处理引用类型可空注解
        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return (typeSymbol.WithNullableAnnotation(NullableAnnotation.NotAnnotated), true);
        }

        return (typeSymbol, false);
    }

    /// <summary>
    /// 获取 C# 类型对应的 JSON Schema 类型。
    /// </summary>
    private static string GetJsonSchemaType(ITypeSymbol typeSymbol)
    {
        // 处理特殊类型
        switch (typeSymbol.SpecialType)
        {
            case SpecialType.System_Boolean:
                return "boolean";
            case SpecialType.System_String:
            case SpecialType.System_Char:
                return "string";
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return "number";
        }

        // 处理数组类型
        if (typeSymbol is IArrayTypeSymbol)
        {
            return "array";
        }

        // 处理集合类型 (IEnumerable<T>, List<T> 等)
        if (typeSymbol is INamedTypeSymbol namedType)
        {
            var fullName = namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fullName.StartsWith("global::System.Collections.Generic.IEnumerable<") ||
                fullName.StartsWith("global::System.Collections.Generic.List<") ||
                fullName.StartsWith("global::System.Collections.Generic.IList<") ||
                fullName.StartsWith("global::System.Collections.Generic.IReadOnlyList<") ||
                fullName.StartsWith("global::System.Collections.Generic.ICollection<"))
            {
                return "array";
            }
        }

        // 默认为 object
        return "object";
    }

    /// <summary>
    /// 为 MCP 工具桥接类添加 CallTool 方法。
    /// </summary>
    public static IAllowMemberDeclaration AddCallToolMethod(
        this IAllowMemberDeclaration builder,
        McpServerToolGeneratingModel model)
    {
        var signature = $"""
            public {(model.GetIsAsync() ? "async " : "")}{G.ValueTask}<{G.CallToolResult}> CallTool(
                {G.JsonElement} jsonArguments,
                {G.JsonSerializerContext} jsonSerializerContext,
                {G.CancellationToken} cancellationToken)
            """;

        // 过滤掉 CancellationToken 参数，因为我们会从外部传入
        var methodParameters = model.Method.Parameters
            .Where(p => !IsCancellationTokenParameter(p))
            .ToArray();

        return builder.AddMethodDeclaration(signature, m => m
            .WithRawDocumentationComment("/// <inheritdoc />")
            .AddRawStatements(methodParameters
                .Select(p => (Parameter: p, Name: p.Name, Type: p.Type,
                    JsonName: NamingHelper.MakeKebabCase(p.Name, true, true), HasDefault: p.HasExplicitDefaultValue))
                .Select(p => $"""
                    var {p.Name} = jsonArguments.TryGetProperty("{p.JsonName}", out var {p.Name}Property)
                        ? {p.Name}Property.Deserialize(
                            ({G.JsonTypeInfo}<{p.Type.ToUsingString()}>)jsonSerializerContext.GetTypeInfo(typeof({p.Type.ToNotNullGlobalDisplayString()}))!)
                        : {(p.HasDefault ? FormatDefaultValue(p.Parameter) : $"throw new {G.MissingRequiredArgumentException}(\"{p.JsonName}\")")};
                    """
                ))
            .AddInvokeTargetMethodStatements(model, methodParameters)
        );
    }

    /// <summary>
    /// 添加调用目标方法的语句（包括异步等待和返回值转换）。
    /// </summary>
    private static IAllowStatement AddInvokeTargetMethodStatements(
        this IAllowStatement builder,
        McpServerToolGeneratingModel model,
        IParameterSymbol[] methodParameters)
    {
        var arguments = model.Method.Parameters
            .Select(p => IsCancellationTokenParameter(p)
                ? "cancellationToken"
                : FormatArgument(methodParameters.First(mp => mp.Name == p.Name)))
            .ToArray();

        var methodCall = $"Target.{model.Method.Name}({string.Join(", ", arguments)})";

        builder
            .Condition(model.GetIsAsync(), c => c
                // 异步方法：await 并转换
                .AddRawStatements(
                    $"var result = await {methodCall}.ConfigureAwait(false);",
                    $"return ({G.CallToolResult})result;"
                ))
            .Otherwise(c => c
                // 同步方法：直接转换并返回
                .AddRawStatements(
                    $"var result = {methodCall};",
                    $"return {G.ValueTask}.FromResult(({G.CallToolResult})result);"
                ));

        return builder;
    }

    /// <summary>
    /// 判断参数是否为 CancellationToken 类型。
    /// </summary>
    private static bool IsCancellationTokenParameter(IParameterSymbol parameter)
    {
        return parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == G.CancellationToken;
    }

    private static string FormatArgument(IParameterSymbol parameter)
    {
        var paramName = parameter.Name;
        // 如果是引用类型且非可空，添加 null-forgiving 操作符
        if (!parameter.Type.IsValueType && parameter.NullableAnnotation != NullableAnnotation.Annotated && !parameter.HasExplicitDefaultValue)
        {
            return $"{paramName}!";
        }
        return paramName;
    }

    private static string FormatDefaultValue(IParameterSymbol parameter)
    {
        if (!parameter.HasExplicitDefaultValue)
        {
            return "default";
        }

        var defaultValue = parameter.ExplicitDefaultValue;
        if (defaultValue == null)
        {
            return "null";
        }

        // 处理不同类型的默认值
        return parameter.Type.SpecialType switch
        {
            SpecialType.System_String => $"\"{defaultValue}\"",
            SpecialType.System_Boolean => defaultValue.ToString()!.ToLowerInvariant(),
            SpecialType.System_Char => $"'{defaultValue}'",
            _ => defaultValue.ToString()!,
        };
    }

    /// <summary>
    /// 为属性赋值添加代码（用于对象初始化器）。
    /// </summary>
    private static ISourceTextBuilder AddStringAssignment(
        this ISourceTextBuilder builder,
        string propertyName,
        string? stringValue)
    {
        builder.AddRawText($"{propertyName} = {(stringValue is null ? "null" : $"\"{stringValue}\"")},");
        return builder;
    }

    /// <summary>
    /// 为属性赋值添加代码（用于对象初始化器）。
    /// </summary>
    private static ISourceTextBuilder AddExpressionAssignment(
        this ISourceTextBuilder builder,
        string propertyName,
        string? expression)
    {
        builder.AddRawText($"{propertyName} = {expression ?? "null"},");
        return builder;
    }
}

/// <summary>
/// Schema 成员描述符（统一表示参数和属性）。
/// </summary>
/// <param name="JsonName">JSON 属性名（kebab-case）</param>
/// <param name="Type">类型符号</param>
/// <param name="Description">描述文本（已转义）</param>
/// <param name="IsRequired">是否必需</param>
file record SchemaMemberDescriptor(
    string JsonName,
    ITypeSymbol Type,
    string? Description,
    bool IsRequired
);
