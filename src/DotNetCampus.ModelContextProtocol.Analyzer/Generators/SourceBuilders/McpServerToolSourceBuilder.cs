using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using Microsoft.CodeAnalysis;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators.SourceBuilders;

internal static class McpServerToolSourceBuilder
{
    /// <summary>
    /// 为 MCP 工具桥接类添加 GetToolDefinition 方法。
    /// </summary>
    public static IAllowMemberDeclaration AddGetToolDefinitionMethod(this IAllowMemberDeclaration builder,
        McpServerToolGeneratingModel model)
    {
        return builder
            .AddMethodDeclaration($"public {G.Tool} GetToolDefinition(global::System.Text.Json.Serialization.JsonSerializerContext jsonSerializerContext)", true,
                m => m
                    .WithRawDocumentationComment("/// <inheritdoc />")
                    .AddBracketScope("new()", "{", "}", bs => bs
                        .AddStringAssignment("Name", model.Name)
                        .AddStringAssignment("Title", model.Title)
                        .AddStringAssignment("Description", model.Description)
                        .AddRawStatement(
                            $"InputSchema = GetInputSchema().ToJsonElement(jsonSerializerContext),")
                        .Condition(model.GetReturnTypeSchemaInfo() is not null, output => output
                            .AddRawStatement(
                                $"OutputSchema = GetOutputSchema().ToJsonElement(jsonSerializerContext),"))
                        .EndCondition()
                        .Condition(model.ShouldGenerateAnnotations(), anno => anno
                            .AddStatement("Annotations = ", ",", a => a.AddToolAnnotations(model)))
                        .EndCondition()
                    )
            );
    }
    /// <summary>
    /// 为 MCP 工具桥接类添加 GetInputSchema 方法。
    /// </summary>
    public static IAllowMemberDeclaration AddGetInputSchemaMethod(this IAllowMemberDeclaration builder,
        McpServerToolGeneratingModel model)
    {
        return builder
            .AddMethodDeclaration($"private {G.CompiledJsonSchema} GetInputSchema()", true,
                m => m.AddCompiledJsonSchemaExpression(JsonPropertySchemaInfo.From(model))
            );
    }

    /// <summary>
    /// 为 MCP 工具桥接类添加 GetOutputSchema 方法。
    /// </summary>
    public static IAllowMemberDeclaration AddGetOutputSchemaMethod(this IAllowMemberDeclaration builder,
        McpServerToolGeneratingModel model)
    {
        if (model.GetReturnTypeSchemaInfo() is not { } schemaInfo)
        {
            return builder;
        }

        return builder
            .AddMethodDeclaration($"private {G.CompiledJsonSchema} GetOutputSchema()", true,
                m => m.AddCompiledJsonSchemaExpression(schemaInfo)
            );
    }

    /// <summary>
    /// 为 MCP 工具桥接类添加 CallTool 方法。
    /// </summary>
    public static IAllowMemberDeclaration AddCallToolMethod(
        this IAllowMemberDeclaration builder,
        McpServerToolGeneratingModel model)
    {
        var signature = $"public {(model.GetIsAsync() ? "async " : "")}{G.ValueTask}<{G.CallToolResult}> CallTool({G.IMcpServerCallToolContext} context)";

        return builder.AddMethodDeclaration(signature, m => m
            .WithRawDocumentationComment("/// <inheritdoc />")
            .AddRawStatement("var jsonArguments = context.InputJsonArguments;")
            .AddRawStatement("var jsonSerializerContext = context.JsonSerializerContext;")
            .AddRawStatement("var cancellationToken = context.CancellationToken;")
            .AddRawStatements(model.GetParameters(true).Select(p => GenerateParameterDeserializationStatement(p, model)).OfType<string>())
            .AddInvokeTargetMethodStatements(model)
        );
    }

    /// <summary>
    /// 生成参数反序列化语句。
    /// </summary>
    private static string? GenerateParameterDeserializationStatement(IParameterSymbol parameter, McpServerToolGeneratingModel model)
    {
        var parameterType = parameter.GetParameterType();
        var jsonName = parameter.GetJsonPropertyName();
        var hasDefault = parameter.HasExplicitDefaultValue;

        // 获取参数的 Schema 信息以访问多态类型信息
        var schemaInfo = JsonPropertySchemaInfo.From(parameter);
        var polymorphicInfo = schemaInfo.PolymorphicInfo;

        // 构建多态类型参数
        var typeDiscriminatorPropertyName = polymorphicInfo?.DiscriminatorPropertyName;
        var expectedTypeDiscriminatorValues = polymorphicInfo?.DerivedTypes
            .Select(d => $"\"{d.DiscriminatorValue}\"")
            .ToList();

        return parameterType switch
        {
            // InputObject 类型：直接反序列化整个 jsonArguments
            ToolParameterType.InputObject => $"""
var {parameter.Name} = context.EnsureDeserialize<{parameter.Type.ToNullableDisabledGlobalDisplayString()}>(jsonArguments, "{parameter.Type.ToSimpleDisplayString()}", "{parameter.Type.ToDisplayString()}", {FormatNullableString(typeDiscriminatorPropertyName)}{FormatExpectedTypeDiscriminatorValues(expectedTypeDiscriminatorValues)});
""",
            ToolParameterType.Injected when parameter.Type.IsNullableType => $"""
var {parameter.Name} = context.TryGetService<{parameter.Type.ToUsingString()}>();
""",
            ToolParameterType.Injected => $"""
var {parameter.Name} = context.EnsureGetService<{parameter.Type.ToUsingString()}>("{parameter.Type.ToDisplayString()}");
""",
            ToolParameterType.JsonElement => $"""
var {parameter.Name} = jsonArguments.TryGetProperty("{jsonName}", out var {parameter.Name}Property)
    ? {parameter.Name}Property
    : {(hasDefault ? parameter.GetDefaultValueExpression() : $"throw new {G.McpToolMissingRequiredArgumentException}(\"{jsonName}\")")};
""",
            // Parameter 类型：从 jsonArguments 中提取对应属性
            ToolParameterType.Parameter => $"""
var {parameter.Name} = jsonArguments.TryGetProperty("{jsonName}", out var {parameter.Name}Property)
    ? context.EnsureDeserialize<{parameter.Type.ToNullableDisabledGlobalDisplayString()}>({parameter.Name}Property, "{parameter.Type.ToSimpleDisplayString()}", "{parameter.Type.ToDisplayString()}", {FormatNullableString(typeDiscriminatorPropertyName)}{FormatExpectedTypeDiscriminatorValues(expectedTypeDiscriminatorValues)})
    : {(hasDefault ? parameter.GetDefaultValueExpression() : $"throw new {G.McpToolMissingRequiredArgumentException}(\"{jsonName}\")")};
""",
            _ => null,
        };
    }

    /// <summary>
    /// 格式化可空字符串为 C# 代码。
    /// </summary>
    private static string FormatNullableString(string? value)
    {
        return value is null ? "null" : $"\"{value}\"";
    }

    /// <summary>
    /// 格式化预期类型鉴别器值列表为 C# 代码。
    /// </summary>
    private static string FormatExpectedTypeDiscriminatorValues(List<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return "";
        }

        return $", [{string.Join(", ", values)}]";
    }

    /// <summary>
    /// 添加调用目标方法的语句。
    /// </summary>
    private static TBuilder AddInvokeTargetMethodStatements<TBuilder>(
        this TBuilder builder,
        McpServerToolGeneratingModel model)
        where TBuilder : IAllowStatement
    {
        var arguments = model.GetParameters(true)
            .Select(x => x.GetParameterType() switch
            {
                ToolParameterType.CancellationToken => "cancellationToken",
                ToolParameterType.Context => "context",
                _ => x.RequireNullForgiving() ? $"{x.Name}!" : x.Name,
            });
        var callMethodExpression = $"Target.{model.Method.Name}({string.Join(", ", arguments)})";

        var isAsync = model.GetIsAsync();

        var schemaInfo = model.GetReturnTypeSchemaInfo();
        var typeName = schemaInfo?.PropertyType.ToSimpleDisplayString();
        var typeFullName = schemaInfo?.PropertyType.ToDisplayString();
        var hasStructureReturn = typeName is not null && typeFullName is not null;
        var returnType = model.GetReturnType();
        var isVoid = returnType is null;
        var collectionKind = model.GetCollectionReturnKind();
        var isBareCallToolResult = returnType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == G.CallToolResult;

        // Generate the return expression/statement based on priority
        string code;

        if (isVoid)
        {
            // void / Task / ValueTask
            if (isAsync)
            {
                code = $"""
                    await {callMethodExpression}.ConfigureAwait(false);
                    return {G.CallToolResult}.Empty;
                    """;
            }
            else
            {
                code = $"""
                    {callMethodExpression};
                    return {G.ValueTask}.FromResult({G.CallToolResult}.Empty);
                    """;
            }
        }
        else if (isBareCallToolResult)
        {
            // 裸 CallToolResult — 直接返回，不依赖用户的 JsonSerializerContext
            code = GenerateReturnBlock(isAsync, callMethodExpression, "result");
        }
        else if (collectionKind is CollectionReturnKind.BasicTypeCollection)
        {
            var elementType = model.GetCollectionElementType();
            if (elementType?.ToJsonSchemaTypeInfo().SpecialKind is JsonSpecialType.Enum)
            {
                var elementTypeFullName = elementType.ToNullableDisabledGlobalDisplayString();
                var elementTypeName = elementType.ToSimpleDisplayString();
                code = GenerateReturnBlock(isAsync, callMethodExpression,
                    $"{G.CallToolResult}.FromCollectionJsonStrings(result, context.EnsureJsonTypeInfo<{elementTypeFullName}>(\"{elementTypeName}\", \"{elementTypeFullName}\"))");
            }
            else
            {
                // 基本类型集合 — 每个元素 ToString()
                code = GenerateReturnBlock(isAsync, callMethodExpression,
                    $"{G.CallToolResult}.FromCollection(result, x => $\"{{x}}\")");
            }
        }
        else if (collectionKind is CollectionReturnKind.ObjectCollection)
        {
            // 对象集合 — 每个元素 JSON 序列化
            var elementTypeFullName = model.GetCollectionElementTypeName(true)!;
            var elementTypeName = model.GetCollectionElementTypeName(false)!;
            code = GenerateReturnBlock(isAsync, callMethodExpression,
                $"{G.CallToolResult}.FromCollection(result, x => {G.JsonSerializer}.Serialize(x, context.EnsureJsonTypeInfo<{elementTypeFullName}>(\"{elementTypeName}\", \"{elementTypeFullName}\")))");
        }
        else if (hasStructureReturn)
        {
            // 可结构化对象
            var notNull = returnType!.GetNotNullTypeSymbol();
            var globalTypeFullName = notNull.ToNullableDisabledGlobalDisplayString();
            code = GenerateReturnBlock(isAsync, callMethodExpression,
                $"{G.CallToolResult}.FromResultStructured(result, context.EnsureJsonTypeInfo<{globalTypeFullName}>(\"{typeName}\", \"{typeFullName}\"))");
        }
        else if (returnType is not null && IsObjectLike(returnType))
        {
            // 不可结构化对象
            var notNull = returnType.GetNotNullTypeSymbol();
            var uTypeName = notNull.ToSimpleDisplayString();
            var uTypeFullName = notNull.ToDisplayString();
            var globalTypeFullName = notNull.ToNullableDisabledGlobalDisplayString();
            code = GenerateReturnBlock(isAsync, callMethodExpression,
                $"{G.CallToolResult}.FromResultUnstructured(result, context.EnsureJsonTypeInfo<{globalTypeFullName}>(\"{uTypeName}\", \"{uTypeFullName}\"))");
        }
        else
        {
            // 其他（string / 基本类型 / 枚举 / JsonElement）
            code = GenerateReturnBlock(isAsync, callMethodExpression, GenerateOtherReturnExpression(returnType));
        }

        builder.AddRawStatement(code);
        return builder;
    }

    /// <summary>
    /// 生成标准的 "var result = ...; return ..." 代码块。
    /// </summary>
    private static string GenerateReturnBlock(bool isAsync, string callMethodExpression, string returnExpression)
    {
        if (isAsync)
        {
            return $"""
                var result = await {callMethodExpression}.ConfigureAwait(false);
                return {returnExpression};
                """;
        }
        else
        {
            return $"""
                var result = {callMethodExpression};
                return {G.ValueTask}.FromResult({returnExpression});
                """;
        }
    }

    /// <summary>
    /// 判断类型是否为对象类型（非基本类型、非字符串、非枚举、非 JsonElement）。
    /// </summary>
    private static bool IsObjectLike(ITypeSymbol returnType)
    {
        var notNull = returnType.GetNotNullTypeSymbol();
        var info = notNull.ToJsonSchemaTypeInfo();
        return info.SpecialKind is JsonSpecialType.Object or JsonSpecialType.Dictionary
               && !notNull.IsAnyJsonElementType();
    }

    /// <summary>
    /// 为 string / 基本类型 / 枚举 / JsonElement 生成返回表达式。
    /// </summary>
    private static string GenerateOtherReturnExpression(ITypeSymbol? returnType)
    {
        if (returnType is null)
        {
            return $"{G.CallToolResult}.Empty";
        }

        var notNull = returnType.GetNotNullTypeSymbol();

        // string → FromResult(result)
        if (notNull.SpecialType == SpecialType.System_String)
        {
            return $"{G.CallToolResult}.FromResult(result)";
        }

        // JsonElement / JsonNode → FromResultUnstructured + EnsureJsonTypeInfo
        if (notNull.IsAnyJsonElementType())
        {
            var jsonTypeName = notNull.ToSimpleDisplayString();
            var jsonTypeFullName = notNull.ToDisplayString();
            var jsonGlobalTypeFullName = notNull.ToNullableDisabledGlobalDisplayString();
            return $"{G.CallToolResult}.FromResultUnstructured(result, context.EnsureJsonTypeInfo<{jsonGlobalTypeFullName}>(\"{jsonTypeName}\", \"{jsonTypeFullName}\"))";
        }

        if (notNull.ToJsonSchemaTypeInfo().SpecialKind is JsonSpecialType.Enum)
        {
            var jsonTypeName = notNull.ToSimpleDisplayString();
            var jsonTypeFullName = notNull.ToDisplayString();
            var jsonGlobalTypeFullName = notNull.ToNullableDisabledGlobalDisplayString();
            return $"{G.CallToolResult}.FromResultJsonString(result, context.EnsureJsonTypeInfo<{jsonGlobalTypeFullName}>(\"{jsonTypeName}\", \"{jsonTypeFullName}\"))";
        }

        // 基本类型 / 兜底 → FromResult(result.ToString())
        var toStringExpr = notNull.IsValueType
            ? "result.ToString()"
            : "result?.ToString() ?? \"\"";
        return $"{G.CallToolResult}.FromResult({toStringExpr})";
    }

    /// <summary>
    /// 判断参数是否是非可空引用类型，且参数没有默认值。这种参数需要添加空包容（!）运算符再使用。
    /// </summary>
    private static bool RequireNullForgiving(this IParameterSymbol parameter)
    {
        return !parameter.Type.IsValueType &&
               parameter.NullableAnnotation is not NullableAnnotation.Annotated &&
               !parameter.HasExplicitDefaultValue;
    }

    /// <summary>
    /// 获取此参数的默认值的表达式字符串形式。
    /// </summary>
    private static string GetDefaultValueExpression(this IParameterSymbol parameter)
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

        // 枚举类型需要转换为枚举值
        if (parameter.Type.TypeKind == TypeKind.Enum)
        {
            var enumType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"({enumType}){defaultValue}";
        }

        return parameter.Type.SpecialType switch
        {
            SpecialType.System_String => $"\"{defaultValue}\"",
            SpecialType.System_Boolean => defaultValue.ToString()!.ToLowerInvariant(),
            SpecialType.System_Char => $"'{defaultValue}'",
            _ => defaultValue.ToString()!,
        };
    }

    /// <summary>
    /// 添加字符串属性赋值（用于对象初始化器）。
    /// </summary>
    private static TBuilder AddStringAssignment<TBuilder>(this TBuilder builder,
        string propertyName, string? stringValue)
        where TBuilder : ISourceTextBuilder
    {
        if (stringValue is not null)
        {
            var hasSpecialChars = stringValue.IndexOfAny(['\r', '\n', '\t', '"', '\\']) >= 0;
            var value = hasSpecialChars switch
            {
                true => $"""""
                        """
                        {stringValue}
                        """
                        """"",
                false => $"\"{stringValue}\"",
            };

            builder.AddRawText($"{propertyName} = {value},");
        }
        return builder;
    }

    /// <summary>
    /// 为 Tool 添加 ToolAnnotations 对象初始化表达式。
    /// </summary>
    private static IAllowStatement AddToolAnnotations(this IAllowStatement builder, McpServerToolGeneratingModel model)
    {
        return builder.AddBracketScope($"new {G.ToolAnnotations}", "{", "}", false, bs => bs
            .Condition(model.ReadOnly.HasValue, ro => ro
                .AddRawText($"ReadOnlyHint = {model.ReadOnly!.ToString().ToLowerInvariant()},"))
            .EndCondition()
            .Condition(model.Destructive.HasValue, dest => dest
                .AddRawText($"DestructiveHint = {model.Destructive!.ToString().ToLowerInvariant()},"))
            .EndCondition()
            .Condition(model.Idempotent.HasValue, idem => idem
                .AddRawText($"IdempotentHint = {model.Idempotent!.ToString().ToLowerInvariant()},"))
            .EndCondition()
            .Condition(model.OpenWorld.HasValue, ow => ow
                .AddRawText($"OpenWorldHint = {model.OpenWorld!.ToString().ToLowerInvariant()},"))
            .EndCondition()
        );
    }
}
