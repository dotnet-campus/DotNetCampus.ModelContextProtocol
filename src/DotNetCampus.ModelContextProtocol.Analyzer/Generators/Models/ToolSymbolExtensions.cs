using DotNetCampus.ModelContextProtocol.CompilerServices;
using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// 为 MCP 工具调用的参数符号提供扩展方法。
/// </summary>
public static class ToolSymbolExtensions
{
    /// <param name="parameter">参数符号。</param>
    extension(IParameterSymbol parameter)
    {
        /// <summary>
        /// 判断参数是否为 CancellationToken 类型。
        /// </summary>
        public bool IsCancellationTokenParameter()
        {
            return parameter.Type.ToGlobalDisplayString() == "global::System.Threading.CancellationToken";
        }

        /// <summary>
        /// 判断参数是否为 IMcpServerCallToolContext 类型。
        /// </summary>
        public bool IsContextParameter()
        {
            return parameter.Type.ToGlobalDisplayString() == "global::DotNetCampus.ModelContextProtocol.Servers.IMcpServerCallToolContext";
        }

        /// <summary>
        /// 获取参数的 ToolParameterType。
        /// </summary>
        public ToolParameterType GetParameterType()
        {
            // 检查是否有 ToolParameterAttribute 显式指定类型
            var attribute = parameter.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == typeof(ToolParameterAttribute).FullName);

            if (attribute != null)
            {
                foreach (var namedArg in attribute.NamedArguments)
                {
                    if (namedArg.Key == nameof(ToolParameterAttribute.Type) && namedArg.Value.Value is int typeValue)
                    {
                        return (ToolParameterType)typeValue;
                    }
                }
            }

            // 自动推断类型
            if (parameter.IsCancellationTokenParameter())
            {
                return ToolParameterType.CancellationToken;
            }

            if (parameter.IsContextParameter())
            {
                return ToolParameterType.Context;
            }

            return ToolParameterType.Parameter;
        }

        /// <summary>
        /// 判断参数是否为特殊参数类型（不需要从 JSON 输入中反序列化）。
        /// 包括：CancellationToken、IMcpServerCallToolContext、Injected 等。
        /// </summary>
        public bool IsSpecialParameter()
        {
            var parameterType = parameter.GetParameterType();
            return parameterType != ToolParameterType.Parameter && parameterType != ToolParameterType.InputObject;
        }
    }
}
