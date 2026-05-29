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
            .AddMethodDeclaration($"public {G.Tool} GetToolDefinition({G.CompiledSchemaJsonContext} jsonContext)", true,
                m => m
                    .WithRawDocumentationComment("/// <inheritdoc />")
                    .AddBracketScope("new()", "{", "}", bs => bs
                        .AddStringAssignment("Name", model.Name)
                        .AddStringAssignment("Title", model.Title)
                        .AddStringAssignment("Description", model.Description)
                        .AddRawStatement(
                            $"InputSchema = {G.JsonSerializer}.SerializeToElement(GetInputSchema(jsonContext), jsonContext.CompiledJsonSchema),")
                        .Condition(model.GetReturnTypeSchemaInfo() is not null, output => output
                            .AddRawStatement(
                                $"OutputSchema = {G.JsonSerializer}.SerializeToElement(GetOutputSchema(jsonContext), jsonContext.CompiledJsonSchema),"))
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
            .AddMethodDeclaration($"private {G.CompiledJsonSchema} GetInputSchema({G.CompiledSchemaJsonContext} jsonContext)", true,
                m => m.AddInputSchemaExpression(JsonPropertySchemaInfo.From(model))
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
            .AddMethodDeclaration($"private {G.CompiledJsonSchema} GetOutputSchema({G.CompiledSchemaJsonContext} jsonContext)", true,
                m => m.AddInputSchemaExpression(schemaInfo)
            );
    }

    /// <summary>
    /// 添加生成 InputSchema 的表达式。
    /// </summary>
    private static IAllowStatement AddInputSchemaExpression(this IAllowStatement builder, JsonPropertySchemaInfo info)
    {
        var itemSchema = info.GetItemSchemaOfArrayOrDefault();
        var properties = info.GetProperties();
        var polymorphicDerivedTypes = info.GetPolymorphicDerivedTypes();
        var isJsonElementType = info.PropertyType.IsAnyJsonElementType();

        return builder
            .AddBracketScope($"new {G.CompiledJsonSchema}", "{", "}", true, bs => bs
                .AddPropertyAssignment("Type", info.GetJsonSchemaTypeExpression())
                .AddPropertyAssignment("Default", info.DefaultValueJsonElement)
                .AddStringAssignment("Description", info.GetEnhancedDescription())
                .AddPropertyAssignment("Enum", info.GetJsonEnumNameExpressionOrDefault())
                .Condition(itemSchema is not null, i => i
                    .AddStatement("Items = ", null, c => c.AddInputSchemaExpression(itemSchema!)))
                .EndCondition()
                // 如果是多态类型，只输出 Required 和 AnyOf，不输出 Properties
                .Condition(polymorphicDerivedTypes.Count > 0, poly => poly
                    .AddPropertyAssignment("Required", $"[ \"{info.PolymorphicInfo!.DiscriminatorPropertyName}\" ]")
                    .AddBracketScope("AnyOf = ", "[", "],", rbs => rbs
                        .AddStatements(polymorphicDerivedTypes, (d, derivedType) => d
                            .AddStatement("", ",", c => c.AddPolymorphicDerivedTypeSchema(derivedType, info))
                        )))
                // 非多态类型，正常处理
                .Otherwise(nonPoly => nonPoly
                    .AddPropertyAssignment("Required", info.GetJsonRequiredPropertiesExpressionOrDefault())
                    // object 类型始终输出 Properties（即使为空），以符合 OpenAI API 要求
                    .Condition(info.JsonSchemaType == "object" || properties.Count > 0, i => i
                        .AddBracketScope($"Properties = new {G.Dictionary}<string, {G.CompiledJsonSchema}>", "{", "},", rbs => rbs
                            .AddStatements(properties, (d, p) => d
                                .AddStatement($"[ \"{p.JsonPropertyName}\" ] = ", ",", c => c
                                    .AddInputSchemaExpression(p))
                            )))
                    .EndCondition()
                )
                .EndCondition()
            );
    }

    /// <summary>
    /// 为多态派生类型添加 Schema 表达式（包含鉴别器约束）。
    /// </summary>
    private static IAllowStatement AddPolymorphicDerivedTypeSchema(
        this IAllowStatement builder,
        JsonPropertySchemaInfo derivedType,
        JsonPropertySchemaInfo baseInfo)
    {
        var discriminatorPropertyName = baseInfo.PolymorphicInfo!.DiscriminatorPropertyName;
        var discriminatorValue = baseInfo.PolymorphicInfo.DerivedTypes
            .FirstOrDefault(d => SymbolEqualityComparer.Default.Equals(d.Type, derivedType.PropertyType))
            ?.DiscriminatorValue ?? derivedType.PropertyType.Name;

        var properties = derivedType.GetProperties();

        return builder
            .AddBracketScope($"new {G.CompiledJsonSchema}", "{", "}", true, bs => bs
                .AddPropertyAssignment("Type", null)
                .AddBracketScope($"Properties = new {G.Dictionary}<string, {G.CompiledJsonSchema}>", "{", "},", rbs => rbs
                    .AddStatement($"[ \"{discriminatorPropertyName}\" ] = ", ",", c => c
                        .AddBracketScope($"new {G.CompiledJsonSchema}", "{", "}", false, ds => ds
                            .AddPropertyAssignment("Type", null)
                            .AddStringAssignment("Const", discriminatorValue)
                        ))
                    // 添加派生类型的所有属性
                    .AddStatements(properties, (d, p) => d
                        .AddStatement($"[ \"{p.JsonPropertyName}\" ] = ", ",", c => c
                            .AddInputSchemaExpression(p))
                    )
                )
                .Condition(properties.Any(p => p.IsRequired), req => req
                    .AddPropertyAssignment("Required", derivedType.GetJsonRequiredPropertiesExpressionOrDefault()))
                .EndCondition()
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
        var isVoid = model.GetReturnType() is null;
        var isStringCollection = model.IsStringCollectionReturn();
        var schemaInfo = model.GetReturnTypeSchemaInfo();
        var isStructurable = schemaInfo is not null;
        var isCallToolResultWrapped = model.IsCallToolResultWrapped;
        var typeName = schemaInfo?.PropertyType.ToSimpleDisplayString();
        var typeFullName = schemaInfo?.PropertyType.ToDisplayString();

        // 字符串集合返回类型需要特殊的拆分逻辑
        if (isStringCollection)
        {
            return builder.AddStringCollectionReturnStatements(callMethodExpression, isAsync);
        }

        builder.AddRawStatement((isAsync, isVoid, isStructurable, isCallToolResultWrapped) switch
        {
            // async void (Task/ValueTask without result) - 不应到达（DM0102），保留为安全兜底
            (true, true, _, _) => $"""
                await {callMethodExpression}.ConfigureAwait(false);
                return {G.CallToolResult}.Empty;
                """,
            // async with structured return (Foo → outputSchema + structuredContent)
            (true, false, true, _) => $"""
                var result = await {callMethodExpression}.ConfigureAwait(false);
                return {G.CallToolResult}.FromResultStructured(result).Apply(context, "{typeName}", "{typeFullName}");
                """,
            // async CallToolResult<T> wrapped, unstructured → 用户已构建结果，无需再次包装
            (true, false, false, true) => $"""
                var result = await {callMethodExpression}.ConfigureAwait(false);
                return result;
                """,
            // async unstructured (string, JsonElement, Foo? with Structured=false, etc.)
            (true, false, false, false) => $"""
                var result = await {callMethodExpression}.ConfigureAwait(false);
                return {G.CallToolResult}.FromResultUnstructured(result).Apply(jsonSerializerContext);
                """,
            // sync void - 不应到达（DM0102），保留为安全兜底
            (false, true, _, _) => $"""
                {callMethodExpression};
                return {G.ValueTask}.FromResult({G.CallToolResult}.Empty);
                """,
            // sync with structured return (Foo → outputSchema + structuredContent)
            (false, false, true, _) => $"""
                var result = {callMethodExpression};
                return {G.ValueTask}.FromResult({G.CallToolResult}.FromResultStructured(result).Apply(context, "{typeName}", "{typeFullName}"));
                """,
            // sync CallToolResult<T> wrapped, unstructured → 用户已构建结果，无需再次包装
            (false, false, false, true) => $"""
                var result = {callMethodExpression};
                return {G.ValueTask}.FromResult(result);
                """,
            // sync unstructured (string, JsonElement, Foo? with Structured=false, etc.)
            (false, false, false, false) => $"""
                var result = {callMethodExpression};
                return {G.ValueTask}.FromResult({G.CallToolResult}.FromResultUnstructured(result).Apply(jsonSerializerContext));
                """,
        });
        return builder;
    }

    /// <summary>
    /// 为字符串集合返回类型生成分拆为多个 TextContentBlock 的代码。
    /// </summary>
    private static TBuilder AddStringCollectionReturnStatements<TBuilder>(
        this TBuilder builder,
        string callMethodExpression,
        bool isAsync)
        where TBuilder : IAllowStatement
    {
        var awaitPrefix = isAsync ? "await " : "";
        var configureAwait = isAsync ? ".ConfigureAwait(false)" : "";
        var contentBlock = G.ContentBlock;
        var textContentBlock = G.TextContentBlock;
        var callToolResult = G.CallToolResult;
        var valueTask = G.ValueTask;

        var nullReturn = isAsync
            ? $"return {callToolResult}.Empty;"
            : $"return {valueTask}.FromResult({callToolResult}.Empty);";
        var finalReturn = isAsync
            ? $"return new {callToolResult} {{ Content = blocks, IsError = false }};"
            : $"return {valueTask}.FromResult(new {callToolResult} {{ Content = blocks, IsError = false }});";

        builder
            .AddRawStatement($"var collection = {awaitPrefix}{callMethodExpression}{configureAwait};")
            .AddRawStatement("if (collection is null)")
            .AddRawStatement($"    {nullReturn}")
            .AddRawStatement($"var blocks = new global::System.Collections.Generic.List<{contentBlock}>();")
            .AddRawStatement("foreach (var s in collection)")
            .AddRawStatement("    if (s is not null)")
            .AddRawStatement($"        blocks.Add(new {textContentBlock} {{ Text = s }});")
            .AddRawStatement(finalReturn);
        return builder;
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
    /// 添加属性赋值（用于对象初始化器）。
    /// </summary>
    private static TBuilder AddPropertyAssignment<TBuilder>(this TBuilder builder,
        string property, string? expression)
        where TBuilder : ISourceTextBuilder
    {
        if (expression is not null)
        {
            builder.AddRawText($"{property} = {expression},");
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
