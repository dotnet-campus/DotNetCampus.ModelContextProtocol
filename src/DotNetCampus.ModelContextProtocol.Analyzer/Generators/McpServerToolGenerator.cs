using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
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
                m => m.AddNewInputSchema(model)
            );
    }

    /// <summary>
    /// 添加生成 InputSchema 的语句。
    /// </summary>
    private static IAllowStatement AddNewInputSchema(this IAllowStatement builder, McpServerToolGeneratingModel model)
    {
        var members = model.Parameters
            .Select(SchemaMemberDescriptor.FromParameter)
            .ToArray();

        return builder.AddRawStatement($"return {GenerateSchemaExpression(members, null)};");
    }

    /// <summary>
    /// 生成 schema 表达式（递归）。
    /// </summary>
    private static string GenerateSchemaExpression(SchemaMemberDescriptor[] members, string? description)
    {
        var requiredExpr = GenerateRequiredArrayExpression(members);
        var descriptionExpr = description != null ? $"\"{description}\"" : "null";
        var propertiesExpr = members.Length > 0 ? GeneratePropertiesDictionary(members) : "null";

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
    /// 生成必需属性数组表达式。
    /// </summary>
    private static string GenerateRequiredArrayExpression(SchemaMemberDescriptor[] members)
    {
        var requiredMembers = members
            .Where(m => m.IsRequired)
            .Select(m => $"\"{m.JsonName}\"")
            .ToArray();

        return requiredMembers.Length > 0
            ? $"new[] {{ {string.Join(", ", requiredMembers)} }}"
            : "null";
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
        var (coreType, isNullable) = SchemaMemberDescriptor.UnwrapNullableType(member.Type);
        var jsonSchemaType = SchemaMemberDescriptor.GetJsonSchemaType(coreType);
        var rawTypeExpr = GenerateRawTypeExpression(jsonSchemaType, isNullable);
        var descriptionExpr = member.Description != null ? $"\"{member.Description}\"" : "null";

        // 处理数组类型
        if (jsonSchemaType == "array")
        {
            var elementType = SchemaMemberDescriptor.GetArrayElementType(coreType);
            if (elementType != null)
            {
                var elementDescriptor = new SchemaMemberDescriptor(member.JsonName, elementType, null, true);
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

        // 处理对象类型（递归生成属性）
        if (jsonSchemaType == "object" && coreType is INamedTypeSymbol namedType)
        {
            var properties = SchemaMemberDescriptor.GetObjectProperties(namedType);
            if (properties.Length > 0)
            {
                var propertiesExpr = GeneratePropertiesDictionary(properties);
                var requiredExpr = GenerateRequiredArrayExpression(properties);

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

        // 简单类型
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
    /// 生成 RawType 表达式。
    /// </summary>
    private static string GenerateRawTypeExpression(string jsonSchemaType, bool isNullable)
    {
        return isNullable
            ? $"JsonSerializer.SerializeToElement(new[] {{ \"{jsonSchemaType}\", \"null\" }}, jsonContext.IReadOnlyListString)"
            : $"JsonSerializer.SerializeToElement(\"{jsonSchemaType}\", jsonContext.String)";
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

        var methodParameters = model.Method.Parameters
            .Where(p => !IsCancellationTokenParameter(p))
            .ToArray();

        return builder.AddMethodDeclaration(signature, m => m
            .WithRawDocumentationComment("/// <inheritdoc />")
            .AddRawStatements(GenerateParameterDeserializationStatements(methodParameters))
            .AddInvokeTargetMethodStatements(model, methodParameters)
        );
    }

    /// <summary>
    /// 生成参数反序列化语句。
    /// </summary>
    private static IEnumerable<string> GenerateParameterDeserializationStatements(IParameterSymbol[] parameters)
    {
        return parameters
            .Select(p => (
                Parameter: p,
                Name: p.Name,
                Type: p.Type,
                JsonName: NamingHelper.MakeKebabCase(p.Name, true, true),
                HasDefault: p.HasExplicitDefaultValue
            ))
            .Select(p => $$"""
                var {{p.Name}} = jsonArguments.TryGetProperty("{{p.JsonName}}", out var {{p.Name}}Property)
                    ? {{p.Name}}Property.Deserialize(
                        ({{G.JsonTypeInfo}}<{{p.Type.ToUsingString()}}>)jsonSerializerContext.GetTypeInfo(typeof({{p.Type.ToNotNullGlobalDisplayString()}}))!)
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
        IParameterSymbol[] methodParameters)
    {
        var arguments = GenerateMethodArguments(model.Method.Parameters, methodParameters);
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
        ImmutableArray<IParameterSymbol> allParameters,
        IParameterSymbol[] deserializedParameters)
    {
        return allParameters.Select(p =>
            IsCancellationTokenParameter(p)
                ? "cancellationToken"
                : FormatArgument(deserializedParameters.First(mp => mp.Name == p.Name))
        );
    }

    /// <summary>
    /// 判断参数是否为 CancellationToken 类型。
    /// </summary>
    private static bool IsCancellationTokenParameter(IParameterSymbol parameter)
    {
        return parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == G.CancellationToken;
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
    private static ISourceTextBuilder AddStringAssignment(
        this ISourceTextBuilder builder,
        string propertyName,
        string? stringValue)
    {
        builder.AddRawText($"{propertyName} = {(stringValue is null ? "null" : $"\"{stringValue}\"")},");
        return builder;
    }
}
