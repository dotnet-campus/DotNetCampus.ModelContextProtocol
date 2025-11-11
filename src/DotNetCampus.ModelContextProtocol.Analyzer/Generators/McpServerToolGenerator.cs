using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        var targetFactory = $"global::System.Func<{model.ContainingType.ToUsingString()}>";
        var builder = new SourceTextBuilder(model.Namespace)
            .Using("System.Text.Json")
            .AddTypeDeclaration($"{model.GetGetAccessModifier()} sealed class {model.GetBridgeTypeName()}({targetFactory} targetFactory)", t => t
                .AddBaseTypes("global::DotNetCampus.ModelContextProtocol.CompilerServices.IGeneratedMcpServerToolBridge")
                .WithSummaryComment($"为 <see cref=\"{model.ContainingType.ToUsingString()}.{model.Method.Name}\"/> 方法生成的 MCP 服务器工具桥接类。")
                .AddRawText($"private readonly {targetFactory} _targetFactory = targetFactory;")
                .AddRawText($"private {model.ContainingType.ToUsingString()} Target => _targetFactory();")
                .AddRawText($"/// <inheritdoc />\npublic string ToolName {{ get; }} = \"{model.Name}\";")
                .AddGetToolDefinitionMethod(model)
                .AddCallToolMethod(model)
            );
        return builder.ToString();
    }
}

file static class Extensions
{
    public static IAllowMemberDeclaration AddGetToolDefinitionMethod(this IAllowMemberDeclaration builder, McpServerToolGeneratingModel model)
    {
        return builder.AddMethodDeclaration("public global::DotNetCampus.ModelContextProtocol.Protocol.Tool GetToolDefinition()", m => m
            .WithRawDocumentationComment("/// <inheritdoc />")
            .AddBracketScope("return new()", "{", "};", b => b
                .AddPropertyAssignment("Name", model.Name)
                .AddPropertyAssignment("Title", model.Title)
                .AddPropertyAssignment("Description", model.Description)
            )
        );
    }

    public static IAllowMemberDeclaration AddCallToolMethod(this IAllowMemberDeclaration builder, McpServerToolGeneratingModel model)
    {
        const string valueTask = "global::System.Threading.Tasks.ValueTask";
        const string callToolResult = "global::DotNetCampus.ModelContextProtocol.Protocol.CallToolResult";
        const string jsonElement = "global::System.Text.Json.JsonElement";
        const string jsonSerializerContext = "global::System.Text.Json.Serialization.JsonSerializerContext";
        const string jsonTypeInfo = "global::System.Text.Json.Serialization.Metadata.JsonTypeInfo";
        const string missingRequiredArgumentException = "global::DotNetCampus.ModelContextProtocol.Exceptions.MissingRequiredArgumentException";

        const string signature = $"public {valueTask}<{callToolResult}> CallTool({jsonElement} jsonArguments, {jsonSerializerContext} jsonSerializerContext)";
        return builder.AddMethodDeclaration(signature, m =>
        {
            m.WithRawDocumentationComment("/// <inheritdoc />");

            // 为每个参数生成反序列化代码
            var parameters = model.Method.Parameters;
            foreach (var parameter in parameters)
            {
                var parameterName = parameter.Name;
                var jsonPropertyName = DotNetCampus.ModelContextProtocol.Utils.NamingHelper.MakeKebabCase(parameterName, true, true);
                var parameterType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var hasDefaultValue = parameter.HasExplicitDefaultValue;

                if (hasDefaultValue)
                {
                    // 有默认值的可选参数
                    var defaultValue = FormatDefaultValue(parameter);
                    m.AddRawText($"var {parameterName} = jsonArguments.TryGetProperty(\"{jsonPropertyName}\", out var {parameterName}Property)");
                    m.AddRawText($"    ? {parameterName}Property.Deserialize(");
                    m.AddRawText($"        ({jsonTypeInfo}<{parameterType}>)jsonSerializerContext.GetTypeInfo(typeof({GetTypeForGetTypeInfo(parameter.Type)}))!)");
                    m.AddRawText($"    : {defaultValue};");
                }
                else
                {
                    // 必需参数
                    m.AddRawText($"var {parameterName} = jsonArguments.TryGetProperty(\"{jsonPropertyName}\", out var {parameterName}Property)");
                    m.AddRawText($"    ? {parameterName}Property.Deserialize(");
                    m.AddRawText($"        ({jsonTypeInfo}<{parameterType}>)jsonSerializerContext.GetTypeInfo(typeof({GetTypeForGetTypeInfo(parameter.Type)}))!)");
                    m.AddRawText($"    : throw new {missingRequiredArgumentException}(\"{jsonPropertyName}\");");
                }
            }

            // 调用目标方法
            var methodCall = $"Target.{model.Method.Name}(";
            var argumentList = string.Join(",\n            ", parameters.Select(p =>
            {
                var paramName = p.Name;
                // 如果是引用类型且非可空，添加 null-forgiving 操作符
                if (!p.Type.IsValueType && p.NullableAnnotation != NullableAnnotation.Annotated && !p.HasExplicitDefaultValue)
                {
                    return $"{paramName}!";
                }
                return paramName;
            }));
            m.AddRawText($"var result = {methodCall}");
            if (parameters.Length > 0)
            {
                m.AddRawText($"            {argumentList});");
            }
            else
            {
                m.AddRawText($");");
            }

            // 返回结果
            m.AddRawText($"return {valueTask}.FromResult(result);");
        });
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

    private static string GetTypeForGetTypeInfo(ITypeSymbol type)
    {
        // 对于可空值类型，需要去掉 Nullable<> 包装
        if (type.NullableAnnotation == NullableAnnotation.Annotated && type is INamedTypeSymbol namedType)
        {
            if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                // Nullable<T> -> T
                return namedType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
        }

        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static ISourceTextBuilder AddPropertyAssignment(this ISourceTextBuilder builder, string propertyName, string? stringValue)
    {
        builder.AddRawText($"{propertyName} = {stringValue switch {
            null => "null",
            _ => $"\"{stringValue}\"",
        }},");
        return builder;
    }
}
