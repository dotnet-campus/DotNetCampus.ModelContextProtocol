using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

public record McpServerToolGeneratingModel
{
    public required string? Namespace { get; init; }

    public required INamedTypeSymbol ContainingType { get; init; }

    public required IMethodSymbol Method { get; init; }

    public required string Name { get; init; }

    public required string? Title { get; init; }

    public required string? Description { get; init; }

    public required bool? Destructive { get; init; }

    public required bool? Idempotent { get; init; }

    public required bool? OpenWorld { get; init; }

    public required bool? ReadOnly { get; init; }

    public IReadOnlyList<IParameterSymbol> GetParameters(bool includeAll = false)
    {
        return Method.Parameters
            .Where(p => includeAll || !p.IsSpecialParameter())
            .ToList();
    }

    public IReadOnlyList<JsonPropertySchemaInfo> GetProperties()
    {
        // 检查是否有 InputObject 类型的参数
        var inputObjectParam = Method.Parameters.FirstOrDefault(p => p.GetParameterType() == ToolParameterType.InputObject);
        if (inputObjectParam != null)
        {
            // 如果有 InputObject 参数，直接返回该对象类型的所有属性，不是参数本身
            var inputObjectInfo = JsonPropertySchemaInfo.From(inputObjectParam.Type, "inputObject");
            return inputObjectInfo.GetProperties();
        }

        return Method.Parameters
            .Where(p => !p.IsSpecialParameter())
            .Select(JsonPropertySchemaInfo.From)
            .ToList();
    }

    public static McpServerToolGeneratingModel TryParse(IMethodSymbol methodSymbol, CancellationToken cancellationToken)
    {
        // 解析所有 McpServerToolAttribute 特性中的参数
        var attribute = methodSymbol.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == typeof(McpServerToolAttribute).FullName);
        if (attribute == null)
        {
            throw new InvalidOperationException("Method does not have McpServerToolAttribute");
        }

        return new McpServerToolGeneratingModel
        {
            Namespace = methodSymbol.ContainingType.ContainingNamespace.ToDisplayString(),
            ContainingType = methodSymbol.ContainingType,
            Method = methodSymbol,
            Name = attribute.NamedArguments.GetObjectOrDefault<string>(nameof(McpServerToolAttribute.Name))
                   ?? NamingHelper.MakeSnakeCase(methodSymbol.Name, true, true),
            Title = attribute.NamedArguments.GetObjectOrDefault<string>(nameof(McpServerToolAttribute.Title)),
            Description = attribute.NamedArguments.GetObjectOrDefault<string>(nameof(McpServerToolAttribute.Description))
                          ?? methodSymbol.GetSummaryFromSymbol(),
            Destructive = attribute.NamedArguments.GetValueOrDefault<bool>(nameof(McpServerToolAttribute.Destructive)),
            Idempotent = attribute.NamedArguments.GetValueOrDefault<bool>(nameof(McpServerToolAttribute.Idempotent)),
            OpenWorld = attribute.NamedArguments.GetValueOrDefault<bool>(nameof(McpServerToolAttribute.OpenWorld)),
            ReadOnly = attribute.NamedArguments.GetValueOrDefault<bool>(nameof(McpServerToolAttribute.ReadOnly)),
        };
    }

    public bool GetIsAsync() => IsTaskLikeReturnType(Method.ReturnType);

    public string GetGetAccessModifier()
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
    /// <returns>桥接类的名称。</returns>
    public string GetBridgeTypeName()
    {
        var name = ContainingType.ToDeclarationNestedDisplayString().Replace('.', '_');
        return $"{name}_{Method.Name}_Bridge";
    }

    /// <summary>
    /// 获取方法返回值的实际类型（去除 Task/ValueTask 包装）。
    /// </summary>
    /// <returns>返回值的实际类型，如果是 void/Task/ValueTask 则返回 null。</returns>
    public ITypeSymbol? GetReturnType()
    {
        var returnType = Method.ReturnType;

        // 如果是 Task 或 ValueTask，提取其泛型参数
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
            IsTaskLikeReturnType(returnType))
        {
            return null;
        }

        return returnType;
    }

    /// <summary>
    /// 获取返回值的 JsonPropertySchemaInfo，用于生成 OutputSchema。
    /// </summary>
    /// <returns>返回值的 Schema 信息，如果没有结构化返回则为 null。</returns>
    public JsonPropertySchemaInfo? GetReturnTypeSchemaInfo()
    {
        var returnType = GetReturnType();
        if (returnType is null)
        {
            return null;
        }

        var fullName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // 1. string - 没有结构化返回
        if (returnType.SpecialType == SpecialType.System_String)
        {
            return null;
        }

        // 2. CallToolResult - 没有结构化返回
        if (fullName == "global::DotNetCampus.ModelContextProtocol.Protocol.Messages.CallToolResult")
        {
            return null;
        }

        // 3. CallToolResult<T> - 使用 T 的结构化返回
        if (returnType is INamedTypeSymbol { IsGenericType: true } genericType &&
            genericType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
            "global::DotNetCampus.ModelContextProtocol.CompilerServices.CallToolResult<T>")
        {
            var resultType = genericType.TypeArguments[0];
            return JsonPropertySchemaInfo.From(resultType, "result");
        }

        // 4. 可序列化的对象 - 使用此对象的结构化返回
        return JsonPropertySchemaInfo.From(returnType, "result");
    }

    /// <summary>
    /// 获取用于传递给 Structure 方法的类型名称。
    /// </summary>
    /// <param name="fullName">获取完全限定名还是简单名称。</param>
    /// <returns>类型名称，如果没有结构化返回则为 null。</returns>
    public string? GetReturnTypeName(bool fullName)
    {
        var schemaInfo = GetReturnTypeSchemaInfo();
        return fullName
            ? schemaInfo?.PropertyType.ToDisplayString()
            : schemaInfo?.PropertyType.ToSimpleDisplayString();
    }

    /// <summary>
    /// 判断返回类型是否为 Task 或 ValueTask。
    /// </summary>
    private static bool IsTaskLikeReturnType(ITypeSymbol returnType)
    {
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
    /// 判断是否需要生成 ToolAnnotations。
    /// </summary>
    public bool ShouldGenerateAnnotations()
    {
        // 如果任何注解属性被显式设置，则生成 ToolAnnotations
        return Destructive.HasValue ||
               Idempotent.HasValue ||
               OpenWorld.HasValue ||
               ReadOnly.HasValue;
    }
}
