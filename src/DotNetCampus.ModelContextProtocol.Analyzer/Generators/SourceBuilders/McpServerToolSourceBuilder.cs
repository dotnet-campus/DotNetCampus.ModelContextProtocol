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

        return builder
            .AddBracketScope($"new {G.CompiledJsonSchema}", "{", "}", true, bs => bs
                .AddPropertyAssignment("Type", info.GetJsonSchemaTypeExpression())
                .AddPropertyAssignment("Default", info.DefaultValueJsonElement)
                .AddStringAssignment("Description", info.Description)
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
                    .Condition(properties.Count > 0, i => i
                        .AddBracketScope($"Properties = new {G.Dictionary}<string, {G.CompiledJsonSchema}>", "{", "},", rbs => rbs
                            .AddStatements(properties, (d, p) => d
                                .AddStatement($"[ \"{p.JsonPropertyName}\" ] = ", ",", c => c
                                    .AddInputSchemaExpression(p))
                            )))
                    .EndCondition())
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
            .AddRawStatement($"{G.JsonElement} jsonArguments = context.InputJsonArguments;")
            .AddRawStatement($"{G.JsonSerializerContext} jsonSerializerContext = context.JsonSerializerContext;")
            .AddRawStatement($"{G.CancellationToken} cancellationToken = context.CancellationToken;")
            .AddRawStatements(model.GetParameters(true).Select(GenerateParameterDeserializationStatement).OfType<string>())
            .AddInvokeTargetMethodStatements(model)
        );
    }

    /// <summary>
    /// 生成参数反序列化语句。
    /// </summary>
    private static string? GenerateParameterDeserializationStatement(IParameterSymbol parameter)
    {
        var parameterType = parameter.GetParameterType();
        var jsonName = parameter.GetJsonPropertyName();
        var hasDefault = parameter.HasExplicitDefaultValue;

        return parameterType switch
        {
            // InputObject 类型：直接反序列化整个 jsonArguments
            ToolParameterType.InputObject => $"""
var {parameter.Name} = jsonArguments.Deserialize({G.McpToolJsonTypeInfoNotFoundException}.EnsureTypeInfo<{parameter.Type.ToUsingString()}>(context, "{parameter.Type.ToSimpleDisplayString()}", "{parameter.Type.ToDisplayString()}"));
""",
            ToolParameterType.Injected when parameter.Type.IsNullableType => $"""
var {parameter.Name} = context.TryGetMcpToolService<{parameter.Type.ToUsingString()}>();
""",
            ToolParameterType.Injected => $"""
var {parameter.Name} = context.GetRequiredMcpToolService<{parameter.Type.ToUsingString()}>("{parameter.Type.ToDisplayString()}");
""",
            ToolParameterType.JsonElement => $"""
var {parameter.Name} = jsonArguments.TryGetProperty("{jsonName}", out var {parameter.Name}Property)
    ? {parameter.Name}Property
    : {(hasDefault ? parameter.GetDefaultValueExpression() : $"throw new {G.McpToolMissingRequiredArgumentException}(\"{jsonName}\")")};
""",
            // Parameter 类型：从 jsonArguments 中提取对应属性
            ToolParameterType.Parameter => $"""
var {parameter.Name} = jsonArguments.TryGetProperty("{jsonName}", out var {parameter.Name}Property)
    ? {parameter.Name}Property.Deserialize({G.McpToolJsonTypeInfoNotFoundException}.EnsureTypeInfo<{parameter.Type.ToUsingString()}>(context, "{parameter.Type.ToSimpleDisplayString()}", "{parameter.Type.ToDisplayString()}"))
    : {(hasDefault ? parameter.GetDefaultValueExpression() : $"throw new {G.McpToolMissingRequiredArgumentException}(\"{jsonName}\")")};
""",
            _ => null,
        };
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
        var typeName = model.GetReturnTypeName(false);
        var typeFullName = model.GetReturnTypeName(true);
        var hasStructureReturn = typeName is not null && typeFullName is not null;
        var isVoid = model.GetReturnType() is null;

        builder.AddRawStatement((isAsync, isVoid, hasStructureReturn) switch
        {
            // async void (Task/ValueTask without result)
            (true, true, _) => $"""
                await {callMethodExpression}.ConfigureAwait(false);
                return {G.CallToolResult}.Empty;
                """,
            // async with structured return
            (true, false, true) => $"""
                var result = await {callMethodExpression}.ConfigureAwait(false);
                return (({G.CallToolResult})result).Structure(context, "{typeName}", "{typeFullName}");
                """,
            // async without structured return
            (true, false, false) => $"""
                var result = await {callMethodExpression}.ConfigureAwait(false);
                return (({G.CallToolResult})result).Structure(jsonSerializerContext);
                """,
            // sync void
            (false, true, _) => $"""
                {callMethodExpression};
                return {G.ValueTask}.FromResult({G.CallToolResult}.Empty);
                """,
            // sync with structured return
            (false, false, true) => $"""
                var result = {callMethodExpression};
                return {G.ValueTask}.FromResult(({G.CallToolResult}.FromResult(result)).Structure(context, "{typeName}", "{typeFullName}"));
                """,
            // sync without structured return
            (false, false, false) => $"""
                var result = {callMethodExpression};
                return {G.ValueTask}.FromResult(({G.CallToolResult}.FromResult(result)).Structure(jsonSerializerContext));
                """,
        });
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
