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
            .AddMethodDeclaration(
                $"public {G.Tool} GetToolDefinition({G.InputSchemaJsonObjectJsonContext} jsonContext)",
                m => m
                    .WithRawDocumentationComment("/// <inheritdoc />")
                    .AddBracketScope("return new()", "{", "};", b => b
                        .AddStringAssignment("Name", model.Name)
                        .AddStringAssignment("Title", model.Title)
                        .AddStringAssignment("Description", model.Description)
                        .AddRawText(
                            $"InputSchema = {G.JsonSerializer}.SerializeToElement(GetInputSchema(jsonContext), jsonContext.InputSchemaJsonObject),")
                    )
            )
            .AddMethodDeclaration(
                $"private {G.InputSchemaJsonObject} GetInputSchema({G.InputSchemaJsonObjectJsonContext} jsonContext)",
                m => m
                    .AddRawText("return")
                    .AddNewInputSchema(model)
            );
    }

    private static IAllowStatements AddNewInputSchema(this IAllowStatements builder, McpServerToolGeneratingModel model)
    {
        // 生成包含所有参数的 object 类型 schema
        var requiredParams = model.Parameters
            .Where(p => p.IsRequired)
            .Select(p => $"\"{p.JsonName}\"")
            .ToArray();

        var propertyEntries = model.Parameters
            .Select(p => $"{{ \"{p.JsonName}\", {GeneratePropertySchemaExpression(p)} }}")
            .ToArray();

        return builder
            .AddBracketScope("new()", "{", "};", b => b
                .AddExpressionAssignment("RawType",
                    "JsonSerializer.SerializeToElement(\"object\", jsonContext.String)")
                .AddStringAssignment("Default", null)
                .AddStringAssignment("Description", null)
                .AddExpressionAssignment("Enum", "null")
                .AddExpressionAssignment("Items", "null")
                .Condition(requiredParams.Length > 0, req => req
                    .AddExpressionAssignment("Required",
                        $"new[] {{ {string.Join(", ", requiredParams)} }}"))
                .Otherwise(noReq => noReq
                    .AddExpressionAssignment("Required", "null"))
                .EndCondition()
                .Condition(propertyEntries.Length > 0, props =>
                {
                    props.AddRawText($"Properties = new Dictionary<string, {G.InputSchemaJsonObject}>");
                    props.AddRawText("{");
                    foreach (var entry in propertyEntries)
                    {
                        props.AddRawText($"    {entry},");
                    }
                    props.AddRawText("},");
                })
                .Otherwise(noProps => noProps
                    .AddExpressionAssignment("Properties", "null"))
                .EndCondition()
            );
    }

    /// <summary>
    /// 生成单个属性的 schema 表达式。
    /// </summary>
    private static string GeneratePropertySchemaExpression(ToolParameterModel parameter)
    {
        var typeSymbol = parameter.Type;
        var description = parameter.GetJsonEscapedDescription();

        // 处理可空类型
        var (coreType, isNullable) = UnwrapNullableType(typeSymbol);

        // 获取 JSON Schema 类型
        var jsonSchemaType = GetJsonSchemaType(coreType);

        // 生成 RawType 表达式
        var rawTypeExpr = isNullable
            ? $"JsonSerializer.SerializeToElement(new[] {{ \"{jsonSchemaType}\", \"null\" }}, jsonContext.IReadOnlyListString)"
            : $"JsonSerializer.SerializeToElement(\"{jsonSchemaType}\", jsonContext.String)";

        // 生成描述表达式
        var descriptionExpr = description != null ? $"\"{description}\"" : "null";

        // 处理数组类型的 Items
        string? itemsExpr = null;
        if (jsonSchemaType == "array")
        {
            var elementType = GetArrayElementType(coreType);
            if (elementType != null)
            {
                // 递归生成数组元素的 schema
                var elementParam = new ToolParameterModel
                {
                    Name = parameter.Name,
                    JsonName = parameter.JsonName,
                    Type = elementType,
                    TypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    IsRequired = true,
                    Description = null
                };
                itemsExpr = GeneratePropertySchemaExpression(elementParam);
            }
        }

        // 构建完整的 schema 表达式
        if (itemsExpr != null)
        {
            return $"new {G.InputSchemaJsonObject} {{ RawType = {rawTypeExpr}, Description = {descriptionExpr}, Items = {itemsExpr} }}";
        }
        else
        {
            return $"new {G.InputSchemaJsonObject} {{ RawType = {rawTypeExpr}, Description = {descriptionExpr} }}";
        }
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
    private static IAllowStatements AddInvokeTargetMethodStatements(
        this IAllowStatements builder,
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
