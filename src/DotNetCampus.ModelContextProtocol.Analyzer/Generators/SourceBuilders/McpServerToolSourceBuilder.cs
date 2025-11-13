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
            .AddMethodDeclaration(true,
                $"public {G.Tool} GetToolDefinition({G.InputSchemaJsonObjectJsonContext} jsonContext)",
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
            .AddMethodDeclaration(true, $"private {G.InputSchemaJsonObject} GetInputSchema({G.InputSchemaJsonObjectJsonContext} jsonContext)",
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
        return builder
            .AddBracketScope($"new {G.InputSchemaJsonObject}", "{", "}", true, bs => bs
                .AddPropertyAssignment("RawType", $"JsonSerializer.SerializeToElement(\"{info.JsonSchemaType}\", jsonContext.String)")
                .AddStringAssignment("Default", info.DefaultValue)
                .AddStringAssignment("Description", info.Description)
                .AddPropertyAssignment("Enum", info.GetJsonEnumNameExpressionOrDefault())
                .Condition(itemSchema is not null, i => i
                    .AddStatement("Items = ", null, c => c.AddInputSchemaExpression(itemSchema!)))
                .EndCondition()
                .AddPropertyAssignment("Required", info.GetJsonRequiredPropertiesExpressionOrDefault())
                .Condition(properties.Count > 0, i => i
                    .AddBracketScope($"Properties = new Dictionary<string, {G.InputSchemaJsonObject}>", rbs => rbs
                        .AddStatements(properties, (d, p) => d
                            .AddStatement($"[ \"{p.JsonPropertyName}\" ] = ", ",", c => c
                                .AddInputSchemaExpression(p))
                        )))
                .EndCondition());
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
            .AddRawStatements(GenerateParameterDeserializationStatements(methodParameters))
            .AddInvokeTargetMethodStatements(model, methodParameters)
        );
    }

    /// <summary>
    /// 生成参数反序列化语句。
    /// </summary>
    private static IEnumerable<string> GenerateParameterDeserializationStatements(IReadOnlyList<IParameterSymbol> parameters)
    {
        return parameters
            .Select(p => (
                Parameter: p,
                Name: p.Name,
                Type: p.Type,
                JsonName: NamingHelper.MakeCamelCase(p.Name),
                HasDefault: p.HasExplicitDefaultValue
            ))
            .Select(p => $$"""
                 var {{p.Name}} = jsonArguments.TryGetProperty("{{p.JsonName}}", out var {{p.Name}}Property)
                     ? {{p.Name}}Property.Deserialize(({{G.JsonTypeInfo}}<{{p.Type.ToUsingString()}}>)jsonSerializerContext.GetTypeInfo(typeof({{p.Type.ToNotNullGlobalDisplayString()}}))!)
                     : {{(p.HasDefault ? FormatDefaultValue(p.Parameter) : $"throw new {G.MissingRequiredArgumentException}(\"{p.JsonName}\")")}};
                 """
            );
    }

    /// <summary>
    /// 添加调用目标方法的语句。
    /// </summary>
    private static IAllowStatement AddInvokeTargetMethodStatements(
        this IAllowStatement builder,
        McpServerToolGeneratingModel model,
        IReadOnlyList<IParameterSymbol> methodParameters)
    {
        var arguments = GenerateMethodArguments(model.GetParameters(true), methodParameters);
        var methodCall = $"Target.{model.Method.Name}({string.Join(", ", arguments)})";

        builder
            .Condition(model.GetIsAsync(), async => async
                .AddRawStatements(
                    $"var result = await {methodCall}.ConfigureAwait(false);",
                    $"return ({G.CallToolResult})result;"
                ))
            .Otherwise(sync => sync
                .AddRawStatements(
                    $"var result = {methodCall};",
                    $"return {G.ValueTask}.FromResult(({G.CallToolResult})result);"
                ));

        return builder;
    }

    /// <summary>
    /// 生成方法调用参数列表。
    /// </summary>
    private static IEnumerable<string> GenerateMethodArguments(
        IReadOnlyList<IParameterSymbol> allParameters,
        IReadOnlyList<IParameterSymbol> deserializedParameters)
    {
        return allParameters.Select(p =>
            p.IsCancellationTokenParameter()
                ? "cancellationToken"
                : FormatArgument(deserializedParameters.First(mp => mp.Name == p.Name))
        );
    }

    /// <summary>
    /// 格式化参数（添加 null-forgiving 操作符）。
    /// </summary>
    private static string FormatArgument(IParameterSymbol parameter)
    {
        var paramName = parameter.Name;
        // 引用类型且非可空，添加 ! 操作符
        if (!parameter.Type.IsValueType &&
            parameter.NullableAnnotation != NullableAnnotation.Annotated &&
            !parameter.HasExplicitDefaultValue)
        {
            return $"{paramName}!";
        }
        return paramName;
    }

    /// <summary>
    /// 格式化参数默认值。
    /// </summary>
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
