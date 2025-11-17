using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 扩展 <see cref="IMcpServerCallToolContext"/> 接口的扩展方法。<br/>
/// Extension methods for the <see cref="IMcpServerCallToolContext"/> interface.
/// </summary>
public static class CompilerExtensions
{
    /// <param name="context">工具调用上下文。Tool invocation context.</param>
    extension(IMcpServerCallToolContext context)
    {
        /// <summary>
        /// 确保将指定的 JSON 属性反序列化为指定类型的对象。
        /// </summary>
        /// <param name="property">要反序列化的 JSON 属性。</param>
        /// <param name="sourceGeneratedJsonTypeName">由源生成器提供的要反序列化的类型名称。</param>
        /// <param name="sourceGeneratedJsonTypeFullName">由源生成器提供的要反序列化的类型完整名称。</param>
        /// <param name="typeDiscriminatorPropertyName">多态类型鉴别器属性名称（如果适用）。</param>
        /// <param name="expectedTypeDiscriminatorValues">预期的多态类型鉴别器值（如果适用）。</param>
        /// <typeparam name="T">要反序列化的目标类型。</typeparam>
        /// <returns>反序列化后的对象。</returns>
        /// <exception cref="McpToolJsonTypeInfoNotFoundException">当指定类型的 JSON 类型信息未找到时引发。</exception>
        /// <exception cref="McpToolMissingRequiredTypeDiscriminatorException">当缺少必需的多态类型鉴别器时引发。</exception>
        public T? EnsureDeserialize<T>(JsonElement property,
            string sourceGeneratedJsonTypeName, string sourceGeneratedJsonTypeFullName,
            string? typeDiscriminatorPropertyName, params ReadOnlySpan<string> expectedTypeDiscriminatorValues)
        {
            var jsonTypeInfo = (JsonTypeInfo<T>?)context.JsonSerializerContext.GetTypeInfo(typeof(T));
            if (jsonTypeInfo is null)
            {
                throw context.McpServer.Context.JsonSerializerTypeName is { } serializerName
                    ? new McpToolJsonTypeInfoNotFoundException(sourceGeneratedJsonTypeName, sourceGeneratedJsonTypeFullName, serializerName)
                    : new McpToolJsonTypeInfoNotFoundException(sourceGeneratedJsonTypeName, sourceGeneratedJsonTypeFullName);
            }

            try
            {
                return property.Deserialize(jsonTypeInfo);
            }
            catch (NotSupportedException)
            {
                // System.NotSupportedException: The JSON payload for polymorphic interface or abstract type 'DotNetCampus.SampleMcpServer.McpTools.PolymorphicBase' must specify a type discriminator.
                throw new McpToolMissingRequiredTypeDiscriminatorException(typeDiscriminatorPropertyName!, expectedTypeDiscriminatorValues.ToArray());
            }
            catch (JsonException ex)
            {
                // System.Text.Json.JsonException: Read unrecognized type discriminator id 'xxx'.
                throw new McpToolMissingRequiredTypeDiscriminatorException(ex, typeDiscriminatorPropertyName!, expectedTypeDiscriminatorValues.ToArray());
            }
        }
    }
}
