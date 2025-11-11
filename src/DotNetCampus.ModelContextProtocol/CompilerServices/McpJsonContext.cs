using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.Core;
using DotNetCampus.ModelContextProtocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol;

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

internal sealed class McpServerToolCompositeJsonContext(JsonSerializerContext externalContext) : JsonSerializerContext(null)
{
    public override JsonTypeInfo? GetTypeInfo(Type type)
    {
        return externalContext.GetTypeInfo(type) ?? McpServerToolJsonContext.Default.GetTypeInfo(type);
    }

    protected override JsonSerializerOptions GeneratedSerializerOptions => externalContext.Options;
}

[JsonSerializable(typeof(InputSchemaJsonObject))]
public partial class InputSchemaJsonObjectJsonContext : JsonSerializerContext;

// 基础类型
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(char))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(sbyte))]
[JsonSerializable(typeof(short))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(uint))]
[JsonSerializable(typeof(ulong))]
[JsonSerializable(typeof(ushort))]
[JsonSerializable(typeof(CallToolResult))]
internal partial class McpServerToolJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(InitializeRequestParams))]
[JsonSerializable(typeof(PingRequestParams))]
internal partial class McpServerRequestJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(InitializeResult))]
[JsonSerializable(typeof(InputSchemaJsonObject))]
[JsonSerializable(typeof(NullResult))]
internal partial class McpServerResponseJsonContext : JsonSerializerContext;
