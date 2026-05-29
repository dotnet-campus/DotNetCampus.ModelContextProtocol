using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;
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

    public required bool? Structured { get; init; }

    /// <summary>
    /// 获取返回值是否原本就是 <c>CallToolResult&lt;T&gt;</c> 包装类型。
    /// 此属性影响代码生成时是否需要调用 <c>FromResultStructured</c> 进行包装。
    /// 由 <see cref="GetReturnType"/> 方法在调用时设置。
    /// </summary>
    public bool IsCallToolResultWrapped { get; private set; }

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
    /// 获取方法返回值的实际类型（循环剥离 Task/ValueTask/CallToolResult 包装）。
    /// </summary>
    /// <returns>返回值的最内层类型，如果是 void/Task/ValueTask 则返回 null。裸 CallToolResult 返回其类型本身。</returns>
    public ITypeSymbol? GetReturnType()
    {
        var returnType = Method.ReturnType;
        var isCallToolResultWrapped = false;

        // 循环剥离 Task<T> / ValueTask<T> / CallToolResult<T>
        while (returnType is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            var fullName = namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fullName is "global::System.Threading.Tasks.Task<TResult>"
                or "global::System.Threading.Tasks.ValueTask<TResult>")
            {
                returnType = namedType.TypeArguments[0];
                continue;
            }

            if (fullName == "global::DotNetCampus.ModelContextProtocol.CompilerServices.CallToolResult<T>")
            {
                isCallToolResultWrapped = true;
                returnType = namedType.TypeArguments[0];
                continue;
            }

            break;
        }

        // 如果是 void、Task 或 ValueTask（无返回值），返回 null
        if (returnType.SpecialType == SpecialType.System_Void ||
            IsTaskLikeReturnType(returnType))
        {
            IsCallToolResultWrapped = false;
            return null;
        }

        IsCallToolResultWrapped = isCallToolResultWrapped;
        return returnType;
    }

    /// <summary>
    /// 获取返回值的 JsonPropertySchemaInfo，用于生成 OutputSchema。
    /// 同时执行两层校验：第一层为 MCP 协议合规性，第二层为结构化内容可行性。
    /// 校验不通过时抛出 <see cref="DiagnosticsException"/>。
    /// </summary>
    /// <returns>返回值的 Schema 信息，如果不需要结构化返回则为 null。</returns>
    public JsonPropertySchemaInfo? GetReturnTypeSchemaInfo()
    {
        var t = GetReturnType();

        // 第一层：MCP 协议合规性

        // 1. void / Task / ValueTask → DM0102
        if (t is null)
        {
            throw new DiagnosticsException(
                Diagnostics.DM0102_McpToolVoidReturnType,
                GetReturnTypeLocation(),
                Method.Name);
        }

        // 2. 裸 CallToolResult → 校验 Structured != true，不生成 outputSchema
        var tFullName = t.GetNotNullTypeSymbol().ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (tFullName == G.CallToolResult)
        {
            if (Structured == true)
            {
                throw new DiagnosticsException(
                    Diagnostics.DM0105_McpToolStructuredNotAllowed,
                    GetReturnTypeLocation(),
                    Method.Name, t.ToDisplayString());
            }
            return null;
        }

        // 获取非空类型和 Schema 信息用于后续判定
        var notNull = t.GetNotNullTypeSymbol();
        var info = notNull.ToJsonSchemaTypeInfo();

        // 3. 基本类型 (bool, int, double 等) → DM0103
        if (info.SpecialKind is JsonSpecialType.Boolean or JsonSpecialType.Integer or JsonSpecialType.Number)
        {
            throw new DiagnosticsException(
                Diagnostics.DM0103_McpToolPrimitiveReturnType,
                GetReturnTypeLocation(),
                Method.Name, t.ToDisplayString());
        }

        // 4. 枚举类型 → DM0104
        if (info.SpecialKind is JsonSpecialType.Enum)
        {
            throw new DiagnosticsException(
                Diagnostics.DM0104_McpToolEnumReturnType,
                GetReturnTypeLocation(),
                Method.Name, t.ToDisplayString());
        }

        // 5. 集合/数组类型
        if (info.SpecialKind is JsonSpecialType.Array)
        {
            var elementType = info.AsArrayItemSymbol();
            if (elementType is not null && elementType.SpecialType == SpecialType.System_String)
            {
                // 字符串集合 → 允许，但禁止 Structured = true，不生成 outputSchema
                if (Structured == true)
                {
                    throw new DiagnosticsException(
                        Diagnostics.DM0105_McpToolStructuredNotAllowed,
                        GetReturnTypeLocation(),
                        Method.Name, t.ToDisplayString());
                }
                return null;
            }

            // 非字符串集合 → DM0101
            throw new DiagnosticsException(
                Diagnostics.DM0101_McpToolCollectionReturnTypeNotSupported,
                GetReturnTypeLocation(),
                Method.Name, t.ToDisplayString());
        }

        // 第二层：结构化内容可行性

        // 6. string / string? → 禁止 Structured = true，不生成 outputSchema
        if (info.SpecialKind is JsonSpecialType.String)
        {
            if (Structured == true)
            {
                throw new DiagnosticsException(
                    Diagnostics.DM0105_McpToolStructuredNotAllowed,
                    GetReturnTypeLocation(),
                    Method.Name, t.ToDisplayString());
            }
            return null;
        }

        // 7. JsonElement / JsonNode 等任意 JSON 类型 → 禁止 Structured = true
        if (notNull.IsAnyJsonElementType())
        {
            if (Structured == true)
            {
                throw new DiagnosticsException(
                    Diagnostics.DM0105_McpToolStructuredNotAllowed,
                    GetReturnTypeLocation(),
                    Method.Name, t.ToDisplayString());
            }
            return null;
        }

        // 8. 对象 / 字典类型
        if (t.IsNullableType)
        {
            // 可空对象 Foo? → 必须显式设置 Structured
            switch (Structured)
            {
                case null:
                    throw new DiagnosticsException(
                        Diagnostics.DM0106_McpToolNullableRequiresStructured,
                        GetReturnTypeLocation(),
                        Method.Name, t.ToDisplayString(), notNull.ToDisplayString());
                case true:
                    throw new DiagnosticsException(
                        Diagnostics.DM0107_McpToolNullableStructuredTrue,
                        GetReturnTypeLocation(),
                        Method.Name, t.ToDisplayString(), notNull.ToDisplayString());
                case false:
                    // 承诺书：放弃结构化输出，接受 null 时的破坏式回退
                    return null;
            }
        }

        // 非空对象 Foo → 默认开启结构化，可通过 Structured = false 关闭
        if (Structured == false)
        {
            return null;
        }

        return JsonPropertySchemaInfo.From(notNull, "result");
    }

    /// <summary>
    /// 获取返回值类型的位置信息，用于诊断错误标注。
    /// 优先标注到返回类型语法节点上，而非整个方法。
    /// </summary>
    private Location GetReturnTypeLocation()
    {
        return (Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()
            as Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax)
            ?.ReturnType.GetLocation()
            ?? Method.Locations.FirstOrDefault()
            ?? Location.None;
    }

    /// <summary>
    /// 判断返回值是否为字符串集合类型（需要拆分为多个 TextContentBlock）。
    /// </summary>
    public bool IsStringCollectionReturn()
    {
        var t = GetReturnType();
        if (t is null)
        {
            return false;
        }

        var notNull = t.GetNotNullTypeSymbol();
        var info = notNull.ToJsonSchemaTypeInfo();
        if (info.SpecialKind is not JsonSpecialType.Array)
        {
            return false;
        }

        var elementType = info.AsArrayItemSymbol();
        return elementType is not null && elementType.SpecialType == SpecialType.System_String;
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
