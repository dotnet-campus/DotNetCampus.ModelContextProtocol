using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using Microsoft.CodeAnalysis;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators.SourceBuilders;

internal static class CompiledJsonSchemaSourceBuilder
{
    public static IAllowMemberDeclaration AddGetCompilerGeneratedJsonSchemaMethod(
        this IAllowMemberDeclaration builder,
        GenerateJsonSchemaGeneratingModel model)
    {
        var typeName = model.Type.ToNullableDisabledGlobalDisplayString();
        var signature = $"{model.GetAccessModifier()} static {G.JsonElement} GetCompilerGeneratedJsonSchema(this {G.JsonTypeInfo}<{typeName}> jsonTypeInfo)";

        return builder.AddMethodDeclaration(signature, m => m
            .WithSummaryComment($"获取为 <see cref=\"{typeName}\"/> 生成的 JSON Schema。")
            .AddRawStatement($"var jsonContext = {G.CompiledSchemaJsonContext}.Default;")
            .AddStatement("var schema = ", ";", s => s
                .AddCompiledJsonSchemaExpression(JsonPropertySchemaInfo.From(model.Type, "schema")))
            .AddRawStatement("return schema.ToJsonElement(jsonContext, jsonTypeInfo);")
        );
    }

    /// <summary>
    /// 添加生成 <see cref="DotNetCampus.ModelContextProtocol.CompilerServices.CompiledJsonSchema"/> 的表达式。
    /// </summary>
    public static IAllowStatement AddCompiledJsonSchemaExpression(this IAllowStatement builder, JsonPropertySchemaInfo info)
    {
        var itemSchema = info.GetItemSchemaOfArrayOrDefault();
        var dictionaryValueSchema = info.GetDictionaryValueSchemaOrDefault();
        var properties = info.GetProperties();
        var polymorphicDerivedTypes = info.GetPolymorphicDerivedTypes();

        return builder
            .AddBracketScope($"new {G.CompiledJsonSchema}", "{", "}", true, bs => bs
                .AddPropertyAssignment("Type", info.GetJsonSchemaTypeExpression())
                .Condition(info.Properties is null, runtimeType => runtimeType
                    .AddPropertyAssignment("RuntimeType", $"typeof({info.PropertyType.GetNotNullTypeSymbol().ToNullableDisabledGlobalDisplayString()})"))
                .EndCondition()
                .AddStringAssignment("RuntimePropertyName", info.RuntimePropertyName)
                .AddPropertyAssignment("Default", info.DefaultValueJsonElement)
                .AddStringAssignment("Description", info.GetEnhancedDescription())
                .AddPropertyAssignment("Enum", info.GetJsonEnumNameExpressionOrDefault())
                .Condition(itemSchema is not null, i => i
                    .AddStatement("Items = ", null, c => c.AddCompiledJsonSchemaExpression(itemSchema!)))
                .EndCondition()
                .Condition(dictionaryValueSchema is not null, d => d
                    .AddStatement($"AdditionalProperties = {G.JsonSerializer}.SerializeToElement(", $", {G.CompiledSchemaJsonContext}.Default.CompiledJsonSchema),", c => c.AddCompiledJsonSchemaExpression(dictionaryValueSchema!)))
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
                                    .AddCompiledJsonSchemaExpression(p))
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
        var discriminator = baseInfo.PolymorphicInfo.DerivedTypes
            .FirstOrDefault(d => SymbolEqualityComparer.Default.Equals(d.Type, derivedType.PropertyType))
            ?.Discriminator;

        var properties = derivedType.GetProperties();

        return builder
            .AddBracketScope($"new {G.CompiledJsonSchema}", "{", "}", true, bs => bs
                .AddPropertyAssignment("Type", null)
                .AddBracketScope($"Properties = new {G.Dictionary}<string, {G.CompiledJsonSchema}>", "{", "},", rbs => rbs
                    .AddStatement($"[ \"{discriminatorPropertyName}\" ] = ", ",", c => c
                        .AddBracketScope($"new {G.CompiledJsonSchema}", "{", "}", false, ds => ds
                            .AddPropertyAssignment("Type", null)
                            .AddPropertyAssignment("Const", discriminator?.ToJsonElementExpression())
                        ))
                    // 添加派生类型的所有属性
                    .AddStatements(properties, (d, p) => d
                        .AddStatement($"[ \"{p.JsonPropertyName}\" ] = ", ",", c => c
                            .AddCompiledJsonSchemaExpression(p))
                    )
                )
                .AddPropertyAssignment("Required", GetPolymorphicDerivedTypeRequiredExpression(derivedType, discriminatorPropertyName))
            );
    }

    private static string GetPolymorphicDerivedTypeRequiredExpression(JsonPropertySchemaInfo derivedType, string discriminatorPropertyName)
    {
        var required = derivedType.GetProperties()
            .Where(p => p.IsRequired)
            .Select(p => p.JsonPropertyName)
            .Prepend(discriminatorPropertyName)
            .ToList();

        return $"[ {string.Join(", ", required.Select(x => $"\"{x}\""))} ]";
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
