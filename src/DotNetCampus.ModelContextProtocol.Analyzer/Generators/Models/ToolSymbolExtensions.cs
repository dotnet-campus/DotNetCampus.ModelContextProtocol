using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Utils;
using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// 为 MCP 工具调用的参数符号提供扩展方法。
/// </summary>
public static class ToolSymbolExtensions
{
    /// <param name="parameter">要扩展的参数符号。</param>
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
        /// 判断参数是否为 IMcpServerCallToolContext 类型。
        /// </summary>
        public bool IsJsonObjectParameter()
        {
            return parameter.Type.ToGlobalDisplayString()
                is "object"
                or "global::System.Object"
                or "global::System.Text.Json.JsonElement"
                or "global::System.Text.Json.Nodes.JsonObject";
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
                    if (namedArg is { Key: nameof(ToolParameterAttribute.Type), Value.Value: int typeValue })
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

            if (parameter.IsJsonObjectParameter())
            {
                return ToolParameterType.JsonObject;
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
            return parameterType switch
            {
                ToolParameterType.Context or ToolParameterType.Injected or ToolParameterType.CancellationToken => true,
                _ => false,
            };
        }

        /// <summary>
        /// 获取参数在 JSON 中的属性名称。优先使用 ToolParameterAttribute.Name，否则使用 camelCase 形式的参数名。
        /// </summary>
        public string GetJsonPropertyName()
        {
            var attribute = parameter.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == typeof(ToolParameterAttribute).FullName);

            if (attribute != null)
            {
                foreach (var namedArg in attribute.NamedArguments)
                {
                    if (namedArg.Key == nameof(ToolParameterAttribute.Name) && namedArg.Value.Value is string name)
                    {
                        return name;
                    }
                }
            }

            return NamingHelper.MakeCamelCase(parameter.Name);
        }

        /// <summary>
        /// 获取参数的描述。优先使用 ToolParameterAttribute.Description，否则从 XML 注释中提取。
        /// </summary>
        public string? GetParameterDescriptionWithAttribute()
        {
            var attribute = parameter.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == typeof(ToolParameterAttribute).FullName);

            if (attribute != null)
            {
                foreach (var namedArg in attribute.NamedArguments)
                {
                    if (namedArg.Key == nameof(ToolParameterAttribute.Description) && namedArg.Value.Value is string description)
                    {
                        return description;
                    }
                }
            }

            return parameter.GetParameterDescription();
        }
    }
}
