using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using DotNetCampus.ModelContextProtocol.Utils;
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

        using var builder = new SourceTextBuilder(model.Namespace)
            .Using("System.Text.Json")
            .AddTypeDeclaration($"{model.GetGetAccessModifier()} sealed class {model.GetBridgeTypeName()}({targetFactory} targetFactory)", t => t
                .AddBaseTypes("global::DotNetCampus.ModelContextProtocol.Servers.IMcpServerTool")
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
                "public global::DotNetCampus.ModelContextProtocol.Protocol.Tool GetToolDefinition(global::DotNetCampus.ModelContextProtocol.CompilerServices.InputSchemaJsonObjectJsonContext jsonContext)",
                m => m
                    .WithRawDocumentationComment("/// <inheritdoc />")
                    .AddBracketScope("return new()", "{", "};", b => b
                        .AddStringAssignment("Name", model.Name)
                        .AddStringAssignment("Title", model.Title)
                        .AddStringAssignment("Description", model.Description)
                        .AddRawText(
                            "InputSchema = global::System.Text.Json.JsonSerializer.SerializeToElement(GetInputSchema(jsonContext), jsonContext.InputSchemaJsonObject),")
                    )
            )
            .AddMethodDeclaration(
                $"private global::DotNetCampus.ModelContextProtocol.Protocol.InputSchemaJsonObject GetInputSchema(global::DotNetCampus.ModelContextProtocol.CompilerServices.InputSchemaJsonObjectJsonContext jsonContext)",
                m => m
                    .AddRawText("return")
                    .AddNewInputSchema(model)
            );
    }

    private static IAllowStatements AddNewInputSchema(this IAllowStatements builder, McpServerToolGeneratingModel model)
    {
        return builder
            .AddBracketScope("new()", "{", "};", b => b
                .AddExpressionAssignment("RawType",
                    /*
                     * 这里只可能有两种情况，对应不可空类型和可空类型
                     * 1. JsonSerializer.SerializeToElement("string|number|...|array", jsonContext.String)
                     * 2. JsonSerializer.SerializeToElement(["string|number|...|array", null], jsonContext.IReadOnlyListString)
                     */
                    "default")
                .AddStringAssignment("Default", null)
                .AddStringAssignment("Description", null)
                .AddExpressionAssignment("Enum",
                    /*
                     * 如果是枚举类型，则这里是枚举所有值的列表
                     */
                    "null")
                .AddExpressionAssignment("Items",
                    /*
                     * 当类型是 array 时，这里有两种情况：
                     * 1. null，即当前类型不是数组类型
                     * 2. 数组项的 InputSchemaJsonObject，可能递归调用 AddNewInputSchema
                     */
                    "null")
                .AddExpressionAssignment("Required",
                    /*
                     * 当类型是 object 时，这里有两种情况：
                     * 1. null，即当前类型没有属性了（例如 String、Int32 等基础类型）或所有属性都是可选的
                     * 1. 一个字符串列表，表示所有必需属性的名称
                     */
                    "null")
                .AddExpressionAssignment("Properties",
                    /*
                     * 当类型是 object 时，这里有两种情况：
                     * 1. null，即当前类型没有属性了（例如 String、Int32 等基础类型）
                     * 2. 一个字典，表示所有属性的名称和对应的 InputSchemaJsonObject，这里可能递归调用 AddNewInputSchema
                     */
                    "null"));
    }

    /// <summary>
    /// 为 MCP 工具桥接类添加 CallTool 方法。
    /// </summary>
    public static IAllowMemberDeclaration AddCallToolMethod(
        this IAllowMemberDeclaration builder,
        McpServerToolGeneratingModel model)
    {
        const string valueTask = "global::System.Threading.Tasks.ValueTask";
        const string callToolResult = "global::DotNetCampus.ModelContextProtocol.Protocol.CallToolResult";
        const string jsonElement = "global::System.Text.Json.JsonElement";
        const string jsonSerializerContext = "global::System.Text.Json.Serialization.JsonSerializerContext";
        const string jsonTypeInfo = "global::System.Text.Json.Serialization.Metadata.JsonTypeInfo";
        const string missingRequiredArgumentException = "global::DotNetCampus.ModelContextProtocol.Exceptions.MissingRequiredArgumentException";
        const string cancellationToken = "global::System.Threading.CancellationToken";

        var signature = $"""
            public {(model.GetIsAsync() ? "async " : "")}{valueTask}<{callToolResult}> CallTool(
                {jsonElement} jsonArguments,
                {jsonSerializerContext} jsonSerializerContext,
                {cancellationToken} cancellationToken)
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
                            ({jsonTypeInfo}<{p.Type.ToUsingString()}>)jsonSerializerContext.GetTypeInfo(typeof({p.Type.ToNotNullGlobalDisplayString()}))!)
                        : {(p.HasDefault ? FormatDefaultValue(p.Parameter) : $"throw new {missingRequiredArgumentException}(\"{p.JsonName}\")")};
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
        const string valueTask = "global::System.Threading.Tasks.ValueTask";
        const string callToolResult = "global::DotNetCampus.ModelContextProtocol.Protocol.CallToolResult";

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
                    $"return ({callToolResult})result;"
                ))
            .Otherwise(c => c
                // 同步方法：直接转换并返回
                .AddRawStatements(
                    $"var result = {methodCall};",
                    $"return {valueTask}.FromResult(({callToolResult})result);"
                ));

        return builder;
    }

    /// <summary>
    /// 判断参数是否为 CancellationToken 类型。
    /// </summary>
    private static bool IsCancellationTokenParameter(IParameterSymbol parameter)
    {
        return parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken";
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
