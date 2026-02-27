using Microsoft.CodeAnalysis;
using G = DotNetCampus.ModelContextProtocol.GlobalTypeNames;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// 为 MCP 资源方法的参数符号提供扩展方法。
/// </summary>
public static class ResourceSymbolExtensions
{
    /// <param name="parameter">要扩展的参数符号。</param>
    extension(IParameterSymbol parameter)
    {
        /// <summary>
        /// 判断参数是否为 IMcpServerReadResourceContext 类型。
        /// </summary>
        public bool IsResourceContextParameter()
        {
            return parameter.Type.ToGlobalDisplayString() == G.IMcpServerReadResourceContext;
        }

        /// <summary>
        /// 判断参数是否为资源的特殊参数类型（不需要从 URI 中提取）。
        /// 包括：CancellationToken、IMcpServerReadResourceContext。
        /// </summary>
        public bool IsResourceSpecialParameter()
        {
            return parameter.IsCancellationTokenParameter() || parameter.IsResourceContextParameter();
        }

        /// <summary>
        /// 获取参数的 C# 类型的完全限定名称。
        /// </summary>
        public string GetFullTypeName()
        {
            return parameter.Type.ToGlobalDisplayString();
        }

        /// <summary>
        /// 获取参数类型对应的 TryParse 方法名称（如果支持）。
        /// </summary>
        /// <returns>TryParse 方法名，如果类型不支持则返回 null。</returns>
        public string? GetTryParseMethodName()
        {
            var typeFullName = parameter.Type.ToGlobalDisplayString();
            var typeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            // 支持 TryParse 的内置类型
            return typeFullName switch
            {
                "int" or "global::System.Int32" => "int.TryParse",
                "long" or "global::System.Int64" => "long.TryParse",
                "short" or "global::System.Int16" => "short.TryParse",
                "byte" or "global::System.Byte" => "byte.TryParse",
                "uint" or "global::System.UInt32" => "uint.TryParse",
                "ulong" or "global::System.UInt64" => "ulong.TryParse",
                "ushort" or "global::System.UInt16" => "ushort.TryParse",
                "sbyte" or "global::System.SByte" => "sbyte.TryParse",
                "float" or "global::System.Single" => "float.TryParse",
                "double" or "global::System.Double" => "double.TryParse",
                "decimal" or "global::System.Decimal" => "decimal.TryParse",
                "bool" or "global::System.Boolean" => "bool.TryParse",
                "global::System.Guid" => "global::System.Guid.TryParse",
                "global::System.DateTime" => "global::System.DateTime.TryParse",
                "global::System.DateTimeOffset" => "global::System.DateTimeOffset.TryParse",
                "global::System.TimeSpan" => "global::System.TimeSpan.TryParse",
                _ => typeName == "string" ? null : $"{typeFullName}.TryParse", // string 不需要 TryParse
            };
        }

        /// <summary>
        /// 判断参数是否为 string 类型（不需要解析）。
        /// </summary>
        public bool IsStringType()
        {
            return parameter.Type.SpecialType == SpecialType.System_String;
        }
    }
}
