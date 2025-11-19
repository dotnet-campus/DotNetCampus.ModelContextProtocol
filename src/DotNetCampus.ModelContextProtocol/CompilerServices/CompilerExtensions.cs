using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 扩展 <see cref="IMcpServerCallToolContext"/> 接口的扩展方法。<br/>
/// Extension methods for the <see cref="IMcpServerCallToolContext"/> interface.
/// </summary>
public static class CompilerExtensions
{
    /// <param name="context">工具调用上下文。<br/>Tool invocation context.</param>
    extension(IMcpServerCallToolContext context)
    {
        /// <summary>
        /// 尝试从依赖注入容器中解析并获取指定类型的服务实例。如果未找到该服务，则返回 <see langword="null"/>。
        /// </summary>
        /// <typeparam name="T">要解析的服务类型。</typeparam>
        /// <returns>指定类型的服务实例。</returns>
        public T? TryGetService<T>()
        {
            return (T?)context.Services.GetService(typeof(T));
        }

        /// <summary>
        /// 从依赖注入容器中解析并获取指定类型的服务实例。
        /// </summary>
        /// <param name="sourceGeneratedServiceTypeName">由源生成器提供的要解析的服务类型名称。</param>
        /// <typeparam name="T">要解析的服务类型。</typeparam>
        /// <returns>指定类型的服务实例。</returns>
        /// <exception cref="McpToolServiceNotFoundException">当指定类型的服务在依赖注入容器中未找到时引发。</exception>
        public T EnsureGetService<T>(string sourceGeneratedServiceTypeName)
        {
            return (T?)context.Services.GetService(typeof(T))
                   ?? throw new McpToolServiceNotFoundException(sourceGeneratedServiceTypeName);
        }

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
            catch (NotSupportedException ex) when (typeDiscriminatorPropertyName is not null && ex.Message.Contains("discriminator"))
            {
                // System.NotSupportedException: The JSON payload for polymorphic interface or abstract type 'xxx' must specify a type discriminator.
                throw new McpToolMissingRequiredTypeDiscriminatorException(typeDiscriminatorPropertyName, expectedTypeDiscriminatorValues.ToArray());
            }
            catch (JsonException ex) when (typeDiscriminatorPropertyName is not null && ex.Message.Contains("discriminator"))
            {
                // System.Text.Json.JsonException: Read unrecognized type discriminator id 'xxx'.
                throw new McpToolMissingRequiredTypeDiscriminatorException(ex, typeDiscriminatorPropertyName, expectedTypeDiscriminatorValues.ToArray());
            }
        }
    }

    /// <param name="context">读取资源的上下文。<br/>The context for reading resources.</param>
    extension(IMcpServerReadResourceContext context)
    {
        /// <summary>
        /// 创建包含指定文本资源内容的 <see cref="ReadResourceResult"/> 实例。
        /// </summary>
        /// <param name="text">要包含的文本资源内容。</param>
        /// <returns><see cref="ReadResourceResult"/> 实例。</returns>
        public ReadResourceResult CreateResult(string text) => new()
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = context.Uri,
                    MimeType = "text/plain",
                    Text = text,
                },
            ],
        };

        /// <summary>
        /// 创建包含多个指定文本资源内容的 <see cref="ReadResourceResult"/> 实例。
        /// </summary>
        /// <param name="texts">要包含的文本资源内容集合。</param>
        /// <returns><see cref="ReadResourceResult"/> 实例。</returns>
        public ReadResourceResult CreateResult(IEnumerable<string> texts) => new()
        {
            Contents =
            [
                .. texts.Select(text => new TextResourceContents
                {
                    Uri = context.Uri,
                    MimeType = "text/plain",
                    Text = text,
                }),
            ],
        };

        /// <summary>
        /// 创建包含指定资源内容的 <see cref="ReadResourceResult"/> 实例。
        /// </summary>
        /// <param name="resourceContents">要包含的资源内容。</param>
        /// <returns><see cref="ReadResourceResult"/> 实例。</returns>
        public ReadResourceResult CreateResult(ResourceContents resourceContents) => new()
        {
            Contents =
            [
                resourceContents,
            ],
        };

        /// <summary>
        /// 创建包含指定资源内容的 <see cref="ReadResourceResult"/> 实例。
        /// </summary>
        /// <param name="resourceContents">要包含的资源内容。</param>
        /// <returns><see cref="ReadResourceResult"/> 实例。</returns>
        public ReadResourceResult CreateResult(IEnumerable<ResourceContents> resourceContents) => new()
        {
            Contents =
            [
                ..resourceContents,
            ],
        };
    }
}
