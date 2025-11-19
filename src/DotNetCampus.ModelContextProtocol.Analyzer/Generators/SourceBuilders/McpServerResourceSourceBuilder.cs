using DotNetCampus.ModelContextProtocol.CodeAnalysis;
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

        builder

            // 添加注释：URI 模板和段列表
            .AddRawStatement($"// URI 模板: \"{model.UriTemplate}\"")
            .AddRawStatement("// URI 段列表:")
            .AddRawStatements(segments.Select((segment, i) => segment.IsStatic
                ? $"//   [{i}] = \"{segment.Content}\" (静态)"
                : $"//   [{i}] = {{{segment.ParameterName}}} (参数，{segment.Parameter!.Type.ToSimpleDisplayString()})"))
            .AddRawStatement("")

            // 生成解析代码
            .AddRawStatement("var uri = context.Uri;");

        // 如果第一个段是静态的且较长，使用 Span 优化
        if (segments.Count > 0 && segments[0].IsStatic && segments[0].Content!.Length > 5)
        {
            builder.AddRawStatement("var span = uri.AsSpan();");
        }

        // 逐段解析
        var position = "0";
        var needsPositionVariable = segments.Count > 1 && segments.Any(s => !s.IsStatic);

        if (needsPositionVariable)
        {
            builder.AddRawStatement($"""
                #pragma warning disable CS0219 // 变量“position”已被赋值，但从未使用过它的值
                var position = 0;
                #pragma warning restore CS0219 // 变量“position”已被赋值，但从未使用过它的值
                """);
        }

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];

            if (segment.IsStatic)
            {
                builder.AddStaticSegmentValidation(segment, i, ref position, segments.Count == 1);
            }
            else
            {
                builder.AddParameterSegmentExtraction(segment, i, ref position, i == segments.Count - 1);
            }
        }

        builder.AddRawStatement("");
        return builder;
    }

    /// <summary>
    /// 添加静态段的验证代码。
    /// </summary>
    private static void AddStaticSegmentValidation<TBuilder>(
        this TBuilder builder,
        UriSegment segment,
        int segmentIndex,
        ref string position,
        bool isOnlySegment)
        where TBuilder : IAllowStatement
    {
        var content = segment.Content!;
        var length = content.Length;
        var constName = $"segment{segmentIndex}Length";

        builder.AddRawStatement($"// 段 {segmentIndex}: 验证静态{(segmentIndex == 0 ? "前缀" : "段")} \"{content}\"");

        if (isOnlySegment)
        {
            // 只有一个静态段（整个 URI 是固定的）
            builder.AddRawStatements($$"""
                if (!uri.Equals("{{content}}", {{G.StringComparison}}.Ordinal))
                {
                    throw new {{G.McpResourceNotFoundException}}(context);
                }
                """);
        }
        else if (segmentIndex == 0)
        {
            // 第一个段（前缀验证）
            builder.AddRawStatements($$"""
                const int {{constName}} = {{length}};
                if (uri.Length <= {{constName}} || !uri.AsSpan(0, {{constName}}).SequenceEqual("{{content}}"))
                {
                    throw new {{G.McpResourceNotFoundException}}(context);
                }
                """);
            position = constName;
        }
        else
        {
            // 中间或末尾的静态段
            builder.AddRawStatements($$"""
                const string segment{{segmentIndex}} = "{{content}}";
                const int {{constName}} = {{length}};
                if (span.Length <= position + {{constName}} || !span.Slice(position, {{constName}}).SequenceEqual(segment{{segmentIndex}}))
                {
                    throw new {{G.McpResourceNotFoundException}}(context);
                }
                position += {{constName}};
                """);
            position = "position";
        }

        builder.AddRawStatement("");
    }

    /// <summary>
    /// 添加参数段的提取代码。
    /// </summary>
    private static void AddParameterSegmentExtraction<TBuilder>(
        this TBuilder builder,
        UriSegment segment,
        int segmentIndex,
        ref string position,
        bool isLastSegment)
        where TBuilder : IAllowStatement
    {
        var paramName = segment.ParameterName!;
        var parameter = segment.Parameter!;
        var paramType = parameter.Type.ToSimpleDisplayString();

        builder.AddRawStatement($"// 段 {segmentIndex}: 提取参数 {{{paramName}}} ({paramType})");

        if (isLastSegment)
        {
            // 最后一个段，直接取到末尾
            if (position == "0")
            {
                builder.AddRawStatement($"var {paramName}Span = uri.AsSpan();");
            }
            else if (position == "position")
            {
                builder.AddRawStatement($"var {paramName}Span = span.Slice(position);");
            }
            else
            {
                // position 是一个常量名（如 segment0Length）
                builder.AddRawStatement($"var {paramName}Span = span.Slice({position});");
            }
        }
        else
        {
            // 不是最后一个段，需要查找下一个分隔符
            var positionRef = position == "position" ? "position" : position;
            builder.AddRawStatements($$"""
                var segment{{segmentIndex}}End = span.Slice({{positionRef}}).IndexOf('/');
                if (segment{{segmentIndex}}End < 0)
                {
                    throw new {{G.McpResourceNotFoundException}}(context);
                }
                var {{paramName}}Span = span.Slice({{positionRef}}, segment{{segmentIndex}}End);
                """);
        }

        // 根据参数类型生成解析代码
        if (parameter.IsStringType())
        {
            // string 类型直接使用
            builder.AddRawStatement($"var {paramName} = {paramName}Span.ToString();");
        }
        else
        {
            // 其他类型使用 TryParse
            var tryParseMethod = parameter.GetTryParseMethodName();
            if (tryParseMethod != null)
            {
                builder.AddRawStatements($$"""
                    if (!{{tryParseMethod}}({{paramName}}Span, out var {{paramName}}))
                    {
                        throw new {{G.McpResourceNotFoundException}}(context);
                    }
                    """);
            }
            else
            {
                // 不支持的类型，生成编译错误
                builder.AddRawStatement($"#error Unsupported parameter type: {paramType}. Please use string, int, or other types that support TryParse.");
            }
        }

        if (!isLastSegment)
        {
            // 更新 position：跳过参数值和分隔符 '/'
            builder.AddRawStatement($"position += segment{segmentIndex}End + 1;");
        }

        builder.AddRawStatement("");
        position = "position";
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
            // 有返回值
            var returnTypeFullName = returnType.ToGlobalDisplayString();

            if (isAsync)
            {
                builder.AddRawStatement($"var result = await {callMethodExpression}.ConfigureAwait(false);");
            }
            else
            {
                builder.AddRawStatement($"var result = {callMethodExpression};");
            }

            // 根据返回类型生成不同的包装代码
            if (returnTypeFullName == "global::DotNetCampus.ModelContextProtocol.Protocol.Messages.ResourceContents")
            {
                // 直接返回 ResourceContents
                var returnStatement = isAsync
                    ? $"return {G.ReadResourceResult}.FromResult(result);"
                    : $"return {G.ValueTask}.FromResult({G.ReadResourceResult}.FromResult(result));";
                builder.AddRawStatement(returnStatement);
            }
            else if (returnType.SpecialType == SpecialType.System_String)
            {
                // string 类型，包装为 TextResourceContents
                var mimeType = FormatNullableString(model.MimeType);
                builder.AddBracketScope(
                    isAsync
                        ? $"return {G.ReadResourceResult}.FromResult(new {G.TextResourceContents}"
                        : $"return {G.ValueTask}.FromResult({G.ReadResourceResult}.FromResult(new {G.TextResourceContents}",
                    "{", isAsync ? "});" : "}));", bs => bs
                        .AddRawText("Uri = context.Uri,")
                        .AddRawText($"MimeType = \"{model.MimeType ?? "text/plain"}\",")
                        .AddRawText("Text = result,")
                );
            }
            else if (returnTypeFullName == "global::System.Byte[]")
            {
                // byte[] 类型，包装为 BlobResourceContents
                var mimeType = FormatNullableString(model.MimeType);
                builder.AddBracketScope(
                    isAsync
                        ? $"return {G.ReadResourceResult}.FromResult(new {G.BlobResourceContents}"
                        : $"return {G.ValueTask}.FromResult({G.ReadResourceResult}.FromResult(new {G.BlobResourceContents}",
                    "{", isAsync ? ");" : "));", bs => bs
                        .AddRawText("Uri = context.Uri,")
                        .AddRawText($"MimeType = {mimeType} ?? \"application/octet-stream\",")
                        .AddRawText($"Blob = {G.Convert}.ToBase64String(result),")
                );
            }
            else
            {
                // 其他可序列化对象，JSON 序列化后包装为 TextResourceContents
                builder.AddRawStatement(
                    $"var json = {G.JsonSerializer}.Serialize(result, context.JsonSerializerContext.GetTypeInfo(typeof({returnTypeFullName})));");
                var mimeType = FormatNullableString(model.MimeType);
                builder.AddBracketScope(
                    isAsync
                        ? $"return {G.ReadResourceResult}.FromResult(new {G.TextResourceContents}"
                        : $"return {G.ValueTask}.FromResult({G.ReadResourceResult}.FromResult(new {G.TextResourceContents}",
                    "{", isAsync ? ");" : "));", bs => bs
                        .AddRawText("Uri = context.Uri,")
                        .AddRawText($"MimeType = {mimeType} ?? \"application/json\",")
                        .AddRawText("Text = json,")
                );
            }
        }

        return builder;
    }

    /// <summary>
    /// 格式化可空字符串为 C# 代码。
    /// </summary>
    private static string FormatNullableString(string? value)
    {
        return value is null ? "null" : $"\"{value}\"";
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
