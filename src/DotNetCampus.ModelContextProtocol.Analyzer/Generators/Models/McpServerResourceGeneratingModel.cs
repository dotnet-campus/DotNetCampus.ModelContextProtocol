using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// 表示 MCP 资源源生成器的生成模型。
/// </summary>
public record McpServerResourceGeneratingModel
{
    public required string? Namespace { get; init; }

    public required INamedTypeSymbol ContainingType { get; init; }

    public required IMethodSymbol Method { get; init; }

    public required string UriTemplate { get; init; }

    public required string Name { get; init; }

    public required string? Title { get; init; }

    public required string? Description { get; init; }

    public required string? MimeType { get; init; }

    public required string? IconSource { get; init; }

    /// <summary>
    /// 获取是否为模板资源（URI 中包含参数占位符）。
    /// </summary>
    public bool IsTemplate => UriTemplate.Contains('{');

    /// <summary>
    /// 获取所有参数（包括特殊参数）。
    /// </summary>
    public IReadOnlyList<IParameterSymbol> GetParameters(bool includeAll = false)
    {
        return Method.Parameters
            .Where(p => includeAll || !p.IsResourceSpecialParameter())
            .ToList();
    }

    /// <summary>
    /// 从方法符号解析生成模型。
    /// </summary>
    public static McpServerResourceGeneratingModel? TryParse(IMethodSymbol methodSymbol, CancellationToken cancellationToken)
    {
        // 解析 McpServerResourceAttribute 特性
        var attribute = methodSymbol.GetAttributes()
            .FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == typeof(McpServerResourceAttribute).FullName);
        if (attribute == null)
        {
            return null;
        }

        // 获取 UriTemplate
        var uriTemplate = attribute.NamedArguments.GetObjectOrDefault<string>(nameof(McpServerResourceAttribute.UriTemplate));
        if (string.IsNullOrWhiteSpace(uriTemplate))
        {
            // 如果未设置 UriTemplate，根据方法参数生成默认 URI
            uriTemplate = GenerateDefaultUriTemplate(methodSymbol);
        }

        if (uriTemplate == null)
        {
            return null; // UriTemplate 无效
        }

        return new McpServerResourceGeneratingModel
        {
            Namespace = methodSymbol.ContainingType.ContainingNamespace.ToDisplayString(),
            ContainingType = methodSymbol.ContainingType,
            Method = methodSymbol,
            UriTemplate = uriTemplate,
            Name = attribute.NamedArguments.GetObjectOrDefault<string>(nameof(McpServerResourceAttribute.Name))
                   ?? NamingHelper.MakePascalCase(methodSymbol.Name),
            Title = attribute.NamedArguments.GetObjectOrDefault<string>(nameof(McpServerResourceAttribute.Title)),
            Description = attribute.NamedArguments.GetObjectOrDefault<string>(nameof(McpServerResourceAttribute.Description))
                          ?? methodSymbol.GetSummaryFromSymbol(),
            MimeType = attribute.NamedArguments.GetObjectOrDefault<string>(nameof(McpServerResourceAttribute.MimeType)),
            IconSource = attribute.NamedArguments.GetObjectOrDefault<string>(nameof(McpServerResourceAttribute.IconSource)),
        };
    }

    /// <summary>
    /// 生成默认的 URI 模板。
    /// </summary>
    private static string GenerateDefaultUriTemplate(IMethodSymbol methodSymbol)
    {
        var typeName = methodSymbol.ContainingType.Name.ToLowerInvariant();
        var methodName = NamingHelper.MakeKebabCase(methodSymbol.Name);

        // 获取非特殊参数
        var parameters = methodSymbol.Parameters
            .Where(p => !p.IsResourceSpecialParameter())
            .ToList();

        if (parameters.Count == 0)
        {
            return $"resource://{typeName}/{methodName}";
        }

        var paramParts = string.Join("/", parameters.Select(p => $"{{{p.Name}}}"));
        return $"resource://{typeName}/{methodName}/{paramParts}";
    }

    /// <summary>
    /// 获取方法是否为异步方法。
    /// </summary>
    public bool GetIsAsync()
    {
        var returnType = Method.ReturnType;
        if (returnType is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var fullName = namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return fullName is "global::System.Threading.Tasks.Task<TResult>"
            or "global::System.Threading.Tasks.ValueTask<TResult>"
            or "global::System.Threading.Tasks.Task"
            or "global::System.Threading.Tasks.ValueTask";
    }

    /// <summary>
    /// 获取访问修饰符。
    /// </summary>
    public string GetAccessModifier()
    {
        var accessibility = (Accessibility)Math.Min((int)ContainingType.DeclaredAccessibility, (int)Method.DeclaredAccessibility);
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => throw new NotSupportedException($"Unsupported accessibility: {Method.DeclaredAccessibility}"),
        };
    }

    /// <summary>
    /// 获取桥接类的名称。
    /// </summary>
    public string GetBridgeTypeName()
    {
        var name = ContainingType.ToDeclarationNestedDisplayString().Replace('.', '_');
        return $"{name}_{Method.Name}_Bridge";
    }

    /// <summary>
    /// 获取方法返回值的实际类型（去除 Task/ValueTask 包装）。
    /// </summary>
    public ITypeSymbol? GetReturnType()
    {
        var returnType = Method.ReturnType;

        // 如果是 Task<T> 或 ValueTask<T>，提取其泛型参数
        if (returnType is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            var fullName = namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fullName is "global::System.Threading.Tasks.Task<TResult>"
                or "global::System.Threading.Tasks.ValueTask<TResult>")
            {
                return namedType.TypeArguments[0];
            }
        }

        // 如果是 void、Task 或 ValueTask（无返回值），返回 null
        if (returnType.SpecialType == SpecialType.System_Void ||
            GetIsAsync() && returnType is INamedTypeSymbol { IsGenericType: false })
        {
            return null;
        }

        return returnType;
    }

    /// <summary>
    /// 解析 URI 模板，返回段列表（静态段和参数段交替）。
    /// </summary>
    public List<UriSegment> ParseUriSegments()
    {
        var segments = new List<UriSegment>();
        var template = UriTemplate;
        var position = 0;

        while (position < template.Length)
        {
            var paramStart = template.IndexOf('{', position);
            if (paramStart < 0)
            {
                // 剩余全是静态段
                if (position < template.Length)
                {
                    segments.Add(new UriSegment
                    {
                        IsStatic = true,
                        Content = template.Substring(position),
                    });
                }
                break;
            }

            // 添加静态段（如果有）
            if (paramStart > position)
            {
                segments.Add(new UriSegment
                {
                    IsStatic = true,
                    Content = template.Substring(position, paramStart - position),
                });
            }

            // 查找参数结束位置
            var paramEnd = template.IndexOf('}', paramStart);
            if (paramEnd < 0)
            {
                throw new InvalidOperationException($"Invalid URI template: unclosed parameter at position {paramStart}");
            }

            // 添加参数段
            var paramName = template.Substring(paramStart + 1, paramEnd - paramStart - 1);
            var parameter = Method.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));

            if (parameter == null)
            {
                throw new InvalidOperationException($"URI template parameter '{paramName}' not found in method parameters");
            }

            segments.Add(new UriSegment
            {
                IsStatic = false,
                ParameterName = paramName,
                Parameter = parameter,
            });

            position = paramEnd + 1;
        }

        return segments;
    }
}

/// <summary>
/// URI 模板的段（静态或参数）。
/// </summary>
public record UriSegment
{
    /// <summary>
    /// 是否为静态段。
    /// </summary>
    public required bool IsStatic { get; init; }

    /// <summary>
    /// 静态段的内容（仅当 IsStatic = true 时有效）。
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// 参数名称（仅当 IsStatic = false 时有效）。
    /// </summary>
    public string? ParameterName { get; init; }

    /// <summary>
    /// 参数符号（仅当 IsStatic = false 时有效）。
    /// </summary>
    public IParameterSymbol? Parameter { get; init; }
}
