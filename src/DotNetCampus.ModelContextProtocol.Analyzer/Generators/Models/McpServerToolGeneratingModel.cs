using System.Diagnostics.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

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

    /// <summary>
    /// Structured 属性的三态值：null=未设置，true=显式true，false=显式false。
    /// </summary>
    public required bool? Structured { get; init; }

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
            Structured = attribute.NamedArguments.GetValueOrDefault<bool>(nameof(McpServerToolAttribute.Structured)),
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
    /// 获取方法返回值的实际类型（剥离 Task/ValueTask 包装）。
    /// </summary>
    /// <returns>返回值的实际类型，如果是 void/Task/ValueTask 则返回 null。</returns>
    public ITypeSymbol? GetReturnType()
    {
        var returnType = Method.ReturnType;

        // 提取 Task<T> / ValueTask<T> 中的 T
        if (returnType is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            var fullName = namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fullName is "global::System.Threading.Tasks.Task<TResult>"
                or "global::System.Threading.Tasks.ValueTask<TResult>")
            {
                returnType = namedType.TypeArguments[0];
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
    /// 获取返回值的集合分类。
    /// </summary>
    /// <returns>集合返回值的分类。</returns>
    public CollectionReturnKind GetCollectionReturnKind()
    {
        var returnType = GetReturnType()?.GetNotNullTypeSymbol();
        if (returnType is null)
        {
            return CollectionReturnKind.None;
        }

        var info = returnType.ToJsonSchemaTypeInfo();
        if (info.SpecialKind is not JsonSpecialType.Array)
        {
            return CollectionReturnKind.None;
        }

        var elementType = info.AsArrayItemSymbol();
        if (elementType is null)
        {
            return CollectionReturnKind.None;
        }

        var elementNotNull = elementType.GetNotNullTypeSymbol();
        var elementInfo = elementNotNull.ToJsonSchemaTypeInfo();

        // 基本类型/字符串/枚举/JsonElement → BasicTypeCollection
        if (elementInfo.SpecialKind is JsonSpecialType.Boolean
            or JsonSpecialType.Integer
            or JsonSpecialType.Number
            or JsonSpecialType.String
            or JsonSpecialType.Enum)
        {
            return CollectionReturnKind.BasicTypeCollection;
        }

        if (elementNotNull.IsAnyJsonElementType())
        {
            return CollectionReturnKind.BasicTypeCollection;
        }

        // 对象元素集合 → ObjectCollection
        return CollectionReturnKind.ObjectCollection;
    }

    /// <summary>
    /// 获取返回值的 JsonPropertySchemaInfo，用于生成 OutputSchema。
    /// </summary>
    /// <returns>返回值的 Schema 信息，如果没有结构化返回则为 null。</returns>
    public JsonPropertySchemaInfo? GetReturnTypeSchemaInfo()
    {
        var t = GetReturnType();

        // void / Task / ValueTask → 不可结构化
        if (t is null)
        {
            ThrowIfStructuredTrue(t);
            return null;
        }

        var notNull = t.GetNotNullTypeSymbol();
        var fullName = notNull.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // 裸 CallToolResult → 不可结构化
        if (fullName == G.CallToolResult)
        {
            ThrowIfStructuredTrue(notNull);
            return null;
        }

        var info = notNull.ToJsonSchemaTypeInfo();

        // 可空对象 Foo? 或对象集合 Foo[] → 需显式 Structured 设置
        if (info.SpecialKind is JsonSpecialType.Array && GetCollectionReturnKind() is CollectionReturnKind.ObjectCollection)
        {
            return HandleNullableOrCollection(notNull);
        }

        if (info.SpecialKind is JsonSpecialType.Object or JsonSpecialType.Dictionary
            && !notNull.IsAnyJsonElementType())
        {
            // 可空对象 Foo?
            if (t.IsNullableType)
            {
                return HandleNullableOrCollection(notNull);
            }

            // 非空对象 Foo → 默认启用结构化
            if (Structured == false)
            {
                return null;
            }

            return JsonPropertySchemaInfo.From(notNull, "result");
        }

        // 其余类型（基本类型/字符串/枚举/JsonElement/基本类型集合/Dictionary<JsonElement>等）均不可结构化
        ThrowIfStructuredTrue(notNull);
        return null;
    }

    /// <summary>
    /// 处理可空对象 Foo? 或对象集合 Foo[] 的 Structured 校验。
    /// </summary>
    private JsonPropertySchemaInfo? HandleNullableOrCollection(ITypeSymbol notNullType)
    {
        switch (Structured)
        {
            case null:
                ThrowDiagnostic(notNullType, Diagnostics.DM0102_McpToolRequiresStructured);
                return null; // unreachable
            case true:
                ThrowDiagnostic(notNullType, Diagnostics.DM0103_McpToolNullableOrCollectionStructuredTrue);
                return null; // unreachable
            case false:
                return null;
        }
    }

    /// <summary>
    /// 如果 Structured 显式设为 true，则抛出 DM0101。
    /// </summary>
    private void ThrowIfStructuredTrue(ITypeSymbol? returnType)
    {
        if (Structured == true)
        {
            ThrowDiagnostic(returnType, Diagnostics.DM0101_McpToolStructuredNotAllowed);
        }
    }

    /// <summary>
    /// 抛出诊断异常。
    /// </summary>
    [DoesNotReturn]
    private void ThrowDiagnostic(ITypeSymbol? returnType, DiagnosticDescriptor descriptor)
    {
        var returnTypeLocation = (Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()
            as MethodDeclarationSyntax)
            ?.ReturnType.GetLocation()
            ?? Method.Locations.FirstOrDefault()
            ?? Location.None;
        throw new DiagnosticsException(
            descriptor,
            returnTypeLocation,
            Method.Name,
            returnType?.ToDisplayString() ?? "void");
    }

    /// <summary>
    /// 获取对象集合的元素类型名称（用于 JSON 序列化）。
    /// </summary>
    /// <param name="fullName">获取完全限定名还是简单名称。</param>
    /// <returns>元素类型名称，如果不是对象集合则返回 null。</returns>
    public string? GetCollectionElementTypeName(bool fullName)
    {
        if (GetCollectionReturnKind() is not CollectionReturnKind.ObjectCollection)
        {
            return null;
        }

        var returnType = GetReturnType()?.GetNotNullTypeSymbol();
        if (returnType is null)
        {
            return null;
        }

        var info = returnType.ToJsonSchemaTypeInfo();
        var elementType = info.AsArrayItemSymbol()?.GetNotNullTypeSymbol();
        if (elementType is null)
        {
            return null;
        }

        return fullName
            ? elementType.ToNullableDisabledGlobalDisplayString()
            : elementType.ToSimpleDisplayString();
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
