using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using DotNetCampus.ModelContextProtocol.Utils;
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
            .AddMethodDeclaration($"public {G.Tool} GetToolDefinition({G.InputSchemaJsonObjectJsonContext} jsonContext)", true,
                m => m
                    .WithRawDocumentationComment("/// <inheritdoc />")
                    .AddBracketScope("new()", "{", "}", bs => bs
                        .AddStringAssignment("Name", model.Name)
                        .AddStringAssignment("Title", model.Title)
                        .AddStringAssignment("Description", model.Description)
                        .AddRawText(
                            $"InputSchema = {G.JsonSerializer}.SerializeToElement(GetInputSchema(jsonContext), jsonContext.InputSchemaJsonObject),")
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
            .AddMethodDeclaration($"private {G.InputSchemaJsonObject} GetInputSchema({G.InputSchemaJsonObjectJsonContext} jsonContext)", true,
                m => m.AddInputSchemaExpression(JsonPropertySchemaInfo.From(model))
            );
    }

    /// <summary>
    /// 添加生成 InputSchema 的表达式。
    /// </summary>
    private static IAllowStatement AddInputSchemaExpression(this IAllowStatement builder, JsonPropertySchemaInfo info)
    {
        var itemSchema = info.GetItemSchemaOfArrayOrDefault();
        var properties = info.GetProperties();
        var polymorphicDerivedTypes = info.GetPolymorphicDerivedTypesOrDefault();

        return builder
            .AddBracketScope($"new {G.InputSchemaJsonObject}", "{", "}", true, bs => bs
                .AddPropertyAssignment("RawType", info.GetJsonSchemaTypeExpression())
                .AddPropertyAssignment("Default", info.DefaultValueJsonElement)
                .AddStringAssignment("Description", info.Description)
                .AddPropertyAssignment("Enum", info.GetJsonEnumNameExpressionOrDefault())
                .Condition(itemSchema is not null, i => i
                    .AddStatement("Items = ", null, c => c.AddInputSchemaExpression(itemSchema!)))
                .EndCondition()
                .AddPropertyAssignment("Required", info.GetJsonRequiredPropertiesExpressionOrDefault())
                .Condition(properties.Count > 0, i => i
                    .AddBracketScope($"Properties = new {G.Dictionary}<string, {G.InputSchemaJsonObject}>", "{", "},", rbs => rbs
                        .AddStatements(properties, (d, p) => d
                            .AddStatement($"[ \"{p.JsonPropertyName}\" ] = ", ",", c => c
                                .AddInputSchemaExpression(p))
                        )))
                .EndCondition()
                .Condition(polymorphicDerivedTypes.Count > 0, i => i
                    .AddBracketScope("AnyOf = ", "[", "],", rbs => rbs
                        .AddStatements(polymorphicDerivedTypes, (d, derivedType) => d
                            .AddStatement("", ",", c => c.AddPolymorphicDerivedTypeSchema(derivedType, info))
                        )))
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
            .AddBracketScope($"new {G.InputSchemaJsonObject}", "{", "}", true, bs => bs
                .AddPropertyAssignment("RawType", "null")
                .AddBracketScope($"Properties = new {G.Dictionary}<string, {G.InputSchemaJsonObject}>", "{", "},", rbs => rbs
                    .AddStatement($"[ \"{discriminatorPropertyName}\" ] = ", ",", c => c
                        .AddBracketScope($"new {G.InputSchemaJsonObject}", "{", "}", false, ds => ds
                            .AddPropertyAssignment("RawType", "null")
                            .AddStringAssignment("Const", discriminatorValue)
                        ))
                    // 添加派生类型的所有属性
                    .AddStatements(properties, (d, p) => d
                        .AddStatement($"[ \"{p.JsonPropertyName}\" ] = ", ",", c => c
                            .AddInputSchemaExpression(p))
                    )
                )
                .Condition(properties.Any(p => p.IsRequired), req => req
                    .AddPropertyAssignment("Required", GetRequiredPropertiesExcludingNullable(properties)))
                .EndCondition()
            );
    }

    /// <summary>
    /// 获取必需属性列表（不包含可空属性）。
    /// </summary>
    private static string? GetRequiredPropertiesExcludingNullable(
        IReadOnlyList<JsonPropertySchemaInfo> properties)
    {
        var requiredNames = properties
            .Where(p => p.IsRequired && !IsNullableReferenceType(p))
            .Select(p => p.JsonPropertyName)
            .ToList();

        return requiredNames.Count == 0
            ? null
            : $"[ {string.Join(", ", requiredNames.Select(x => $"\"{x}\""))} ]";
    }

    /// <summary>
    /// 判断是否为可空引用类型。
    /// </summary>
    private static bool IsNullableReferenceType(JsonPropertySchemaInfo info)
    {
        // 可空值类型已经在 IsNullableValue 中处理
        // 这里检查引用类型的可空性
        return !info.PropertyType.IsValueType &&
               info.PropertyType.NullableAnnotation == NullableAnnotation.Annotated;
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

        var methodParameters = model.GetParameters();
        return builder.AddMethodDeclaration(signature, m => m
            .WithRawDocumentationComment("/// <inheritdoc />")
            .AddRawStatements(model.GetParameters().Select(GenerateParameterDeserializationStatements))
            .AddInvokeTargetMethodStatements(model)
        );
    }

    /// <summary>
    /// 生成参数反序列化语句。
    /// </summary>
    private static string GenerateParameterDeserializationStatements(IParameterSymbol parameter)
    {
        var jsonName = NamingHelper.MakeCamelCase(parameter.Name);
        var hasDefault = parameter.HasExplicitDefaultValue;
        return $"""
            var {parameter.Name} = jsonArguments.TryGetProperty("{jsonName}", out var {parameter.Name}Property)
                ? {parameter.Name}Property.Deserialize({G.JsonTypeInfoNotGeneratedException}.EnsureTypeInfo<{parameter.Type.ToUsingString()}>(jsonSerializerContext))
                : {(hasDefault ? parameter.GetDefaultValueExpression() : $"throw new {G.MissingRequiredArgumentException}(\"{jsonName}\")")};
            """;
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
            .Select(x => x.IsCancellationTokenParameter()
                ? "cancellationToken"
                : x.RequireNullForgiving()
                    ? $"{x.Name}!"
                    : x.Name);
        var callMethodExpression = $"Target.{model.Method.Name}({string.Join(", ", arguments)})";

        builder
            .Condition(model.GetIsAsync(), async => async
                .AddRawStatements(
                    $"var result = await {callMethodExpression}.ConfigureAwait(false);",
                    $"return (({G.CallToolResult})result).Structure(jsonSerializerContext);"
                ))
            .Otherwise(sync => sync
                .AddRawStatements(
                    $"var result = {callMethodExpression};",
                    $"return {G.ValueTask}.FromResult((({G.CallToolResult})result).Structure(jsonSerializerContext));"
                ));

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
}
