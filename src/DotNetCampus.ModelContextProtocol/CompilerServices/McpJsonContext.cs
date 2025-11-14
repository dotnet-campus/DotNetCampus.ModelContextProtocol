using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.Core;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Protocol.Schema;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 以 <see cref="System.Text.Json"/> 作为底层机制支持 MCP 服务器工具的 JSON 序列化器。
/// </summary>
public class McpServerToolJsonSerializer : IMcpServerToolJsonSerializer
{
    internal McpServerToolJsonSerializer()
    {
        JsonSerializerContext = McpServerToolJsonContext.Default;
    }

    /// <summary>
    /// 创建 <see cref="McpServerToolJsonSerializer"/> 的新实例。
    /// </summary>
    /// <param name="jsonSerializerContext">业务端用于业务对象序列化和反序列化的 JSON 序列化上下文。可由源生成器生成。</param>
    public McpServerToolJsonSerializer(JsonSerializerContext jsonSerializerContext)
    {
        JsonSerializerContext = new McpServerToolCompositeJsonContext(jsonSerializerContext);
    }

    /// <summary>
    /// 获取 JSON 序列化上下文。
    /// </summary>
    public JsonSerializerContext? JsonSerializerContext { get; }
}

/// <summary>
/// 表示用于 MCP 服务器工具的 JSON 序列化器。
/// </summary>
public interface IMcpServerToolJsonSerializer
{
}

/// <summary>
/// 合并 <see cref="McpServerToolJsonContext"/> 和外部提供的 <see cref="JsonSerializerContext"/>，以支持业务自定义类型的序列化和反序列化。
/// </summary>
/// <param name="externalContext"></param>
internal sealed class McpServerToolCompositeJsonContext(JsonSerializerContext externalContext) : JsonSerializerContext(null)
{
    public override JsonTypeInfo? GetTypeInfo(Type type)
    {
        return externalContext.GetTypeInfo(type) ?? McpServerToolJsonContext.Default.GetTypeInfo(type);
    }

    protected override JsonSerializerOptions GeneratedSerializerOptions => externalContext.Options;
}

/// <summary>
/// 提供给源生成器使用，用于序列化 MCP 工具的描述信息。
/// </summary>
// 用于编译期可确定的默认值（请参见 JsonPropertySchemaInfo 编译期代码）
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
// 协议类型
[JsonSerializable(typeof(ToolInputSchema))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization)]
public partial class InputSchemaJsonContext : JsonSerializerContext;

/// <summary>
/// 与业务自己定义的 MCP 工具一起合并成 <see cref="McpServerToolJsonSerializer"/> 以序列化和反序列化业务定义的 MCP 工具参数、返回值和相关类型。
/// </summary>
// 基础类型
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(byte?))]
[JsonSerializable(typeof(char))]
[JsonSerializable(typeof(char?))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(decimal?))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(double?))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(float?))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(long?))]
[JsonSerializable(typeof(sbyte))]
[JsonSerializable(typeof(sbyte?))]
[JsonSerializable(typeof(short))]
[JsonSerializable(typeof(short?))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(uint))]
[JsonSerializable(typeof(uint?))]
[JsonSerializable(typeof(ulong))]
[JsonSerializable(typeof(ulong?))]
[JsonSerializable(typeof(ushort))]
[JsonSerializable(typeof(ushort?))]
[JsonSerializable(typeof(CallToolResult))]
[JsonSerializable(typeof(ContentBlock))]
[JsonSerializable(typeof(TextContentBlock))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.Unspecified,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    UseStringEnumConverter = true,
    WriteIndented = false)]
internal partial class McpServerToolJsonContext : JsonSerializerContext;

/// <summary>
/// 提供给 MCP 协议中，服务端收到来自客户端的请求数据时使用的 JSON 序列化上下文。
/// </summary>
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(InitializeRequestParams))]
[JsonSerializable(typeof(PingRequestParams))]
[JsonSerializable(typeof(ListToolsRequestParams))]
[JsonSerializable(typeof(CallToolRequestParams))]
internal partial class McpServerRequestJsonContext : JsonSerializerContext;

/// <summary>
/// 提供给 MCP 协议中，服务端发送给客户端的响应数据时使用的 JSON 序列化上下文。
/// </summary>
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(InitializeResult))]
[JsonSerializable(typeof(ToolInputSchema))]
[JsonSerializable(typeof(EmptyResult))]
[JsonSerializable(typeof(ListToolsResult))]
[JsonSerializable(typeof(CallToolResult))]
[JsonSerializable(typeof(ContentBlock))]
[JsonSerializable(typeof(TextContentBlock))]
[JsonSerializable(typeof(ImageContentBlock))]
[JsonSerializable(typeof(AudioContentBlock))]
[JsonSerializable(typeof(ResourceLinkContentBlock))]
[JsonSerializable(typeof(EmbeddedResourceContentBlock))]
[JsonSerializable(typeof(ResourceContents))]
[JsonSerializable(typeof(TextResourceContents))]
[JsonSerializable(typeof(BlobResourceContents))]
[JsonSerializable(typeof(Annotations))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = false)]
internal partial class McpServerResponseJsonContext : JsonSerializerContext;
