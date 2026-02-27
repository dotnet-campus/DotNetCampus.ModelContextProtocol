using Microsoft.CodeAnalysis;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// 调用是否由工厂委托创建实例。
/// </summary>
public enum WithFactoryInvocationKind
{
    /// <summary>
    /// 使用工厂委托重载（Func&lt;T&gt;）。
    /// </summary>
    WithFactory,

    /// <summary>
    /// 不使用工厂委托重载。
    /// </summary>
    WithoutFactory,
}

internal static class WithFactoryInvocationKindResolver
{
    public static WithFactoryInvocationKind? TryResolve(IMethodSymbol targetMethod, ITypeSymbol targetType)
    {
        var parameters = targetMethod.Parameters;

        if (parameters.Length == 1
            && parameters[0].Type.ToGlobalDisplayString() == G.CreationMode)
        {
            return WithFactoryInvocationKind.WithoutFactory;
        }

        if (parameters is
            [
                {
                    Type: INamedTypeSymbol
                    {
                        Name: "Func",
                        TypeArguments.Length: 1,
                    } funcType,
                },
                _,
            ]
            && SymbolEqualityComparer.Default.Equals(funcType.TypeArguments[0], targetType)
            && parameters[1].Type.ToGlobalDisplayString() == G.CreationMode)
        {
            return WithFactoryInvocationKind.WithFactory;
        }

        return null;
    }
}
