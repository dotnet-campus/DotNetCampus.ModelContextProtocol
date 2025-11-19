using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using Microsoft.CodeAnalysis;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators.SourceBuilders;

/// <summary>
/// MCP 资源桥接类的源代码构建器。
/// </summary>
internal static class McpServerResourceSourceBuilder
{
    /// <summary>
    /// 为 MCP 资源桥接类添加 GetResourceDefinition 方法。
    /// </summary>
    public static IAllowMemberDeclaration AddGetResourceDefinitionMethod(
        this IAllowMemberDeclaration builder,
        McpServerResourceGeneratingModel model)
    {
        var definitionType = model.IsTemplate ? G.ResourceTemplate : G.Resource;

        return builder
            .AddMethodDeclaration($"public object GetResourceDefinition({G.CompiledSchemaJsonContext} jsonContext)", true,
                m => m
                    .WithRawDocumentationComment("/// <inheritdoc />")
                    .AddBracketScope($"new {definitionType}", bs => bs
                        .AddStringAssignment("Name", model.Name)
                        .AddStringAssignment("Title", model.Title)
                        .AddPropertyAssignment(model.IsTemplate ? "UriTemplate" : "Uri", $"\"{model.UriTemplate}\"")
                        .AddStringAssignment("Description", model.Description)
                        .AddStringAssignment("MimeType", model.MimeType)
                    )
            );
    }

    /// <summary>
    /// 为 MCP 资源桥接类添加 ReadResource 方法。
    /// </summary>
    public static IAllowMemberDeclaration AddReadResourceMethod(
        this IAllowMemberDeclaration builder,
        McpServerResourceGeneratingModel model)
    {
        var isAsync = model.GetIsAsync();
        var signature = $"public {(isAsync ? "async " : "")}{G.ValueTask}<{G.ReadResourceResult}> ReadResource({G.IMcpServerReadResourceContext} context)";

        return builder.AddMethodDeclaration(signature, m => m
            .WithRawDocumentationComment("/// <inheritdoc />")
            .AddUriParsingStatements(model)
            .AddInvokeTargetMethodStatements(model)
        );
    }

    /// <summary>
    /// 添加 URI 解析语句（用于模板资源）。
    /// </summary>
    private static TBuilder AddUriParsingStatements<TBuilder>(
        this TBuilder builder,
        McpServerResourceGeneratingModel model)
        where TBuilder : IAllowStatement
    {
        if (!model.IsTemplate)
        {
            // 非模板资源：无需解析，直接调用方法
            return builder;
        }

        var segments = model.ParseUriSegments();

        // 添加注释说明 URI 模板
        builder.AddRawStatement($"// 解析 URI: {model.UriTemplate}");

        // 创建 UriTemplateParser 实例
        builder.AddRawStatement($"var parser = new {G.UriTemplateParser}(context);");
        builder.AddRawStatement("");

        // 逐段解析
        foreach (var segment in segments)
        {
            if (segment.IsStatic)
            {
                // 静态段：匹配验证
                var escapedContent = segment.Content!
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"");
                builder.AddRawStatement($"parser.MatchStaticSegment(\"{escapedContent}\");");
            }
            else
            {
                // 参数段：调用对应的解析方法
                var paramName = segment.ParameterName!;
                var parseMethod = GetParseMethodName(segment.Parameter!);
                builder.AddRawStatement($"var {paramName} = parser.{parseMethod}();");
            }
        }

        // 确保到达 URI 末尾
        builder.AddRawStatement("parser.EnsureEndOfUri();");
        builder.AddRawStatement("");

        return builder;
    }

    /// <summary>
    /// 根据参数类型获取对应的 UriTemplateParser 解析方法名。
    /// </summary>
    private static string GetParseMethodName(IParameterSymbol parameter)
    {
        var type = parameter.Type;


        if (parameter.IsStringType())
        {
            return "ParseStringParameter";
        }

        return type.SpecialType switch
        {
            SpecialType.System_Int32 => "ParseInt32Parameter",
            SpecialType.System_Int64 => "ParseInt64Parameter",
            SpecialType.System_Boolean => "ParseBooleanParameter",
            SpecialType.System_Double => "ParseDoubleParameter",
            SpecialType.System_Decimal => "ParseDecimalParameter",
            _ => type.ToGlobalDisplayString() == "global::System.Guid"
                ? "ParseGuidParameter"
                : "ParseStringParameter", // 默认使用字符串解析
        };
    }

    /// <summary>
    /// 添加调用目标方法的语句。
    /// </summary>
    private static TBuilder AddInvokeTargetMethodStatements<TBuilder>(
        this TBuilder builder,
        McpServerResourceGeneratingModel model)
        where TBuilder : IAllowStatement
    {
        // 构建方法参数
        var arguments = model.GetParameters(true)
            .Select(p => p.IsResourceContextParameter() ? "context"
                : p.IsCancellationTokenParameter() ? "context.CancellationToken"
                : p.Name);

        var callMethodExpression = $"Target.{model.Method.Name}({string.Join(", ", arguments)})";
        var isAsync = model.GetIsAsync();
        var returnType = model.GetReturnType();

        builder.AddRawStatement("// 调用目标方法");

        if (returnType == null)
        {
            // void 返回值
            if (isAsync)
            {
                builder.AddRawStatements($"""
                    await {callMethodExpression}.ConfigureAwait(false);
                    return {G.ReadResourceResult}.Empty;
                    """);
            }
            else
            {
                builder.AddRawStatements($"""
                    {callMethodExpression};
                    return {G.ValueTask}.FromResult({G.ReadResourceResult}.Empty);
                    """);
            }
        }
        else
        {
            // 有返回值，统一使用 FromResult
            if (isAsync)
            {
                builder.AddRawStatements($"""
                    var result = await {callMethodExpression}.ConfigureAwait(false);
                    return context.CreateResult(result);
                    """);
            }
            else
            {
                builder.AddRawStatements($"""
                    var result = {callMethodExpression};
                    return {G.ValueTask}.FromResult(context.CreateResult(result));
                    """);
            }
        }

        return builder;
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
            // 转义字符串中的特殊字符
            var escapedValue = stringValue
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");

            builder.AddRawText($"{propertyName} = \"{escapedValue}\",");
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
