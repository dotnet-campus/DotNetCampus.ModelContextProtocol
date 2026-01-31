using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Tests.McpTools;

/// <summary>
/// 用于测试工具的 JSON 序列化上下文。
/// 包含所有测试工具使用的复杂类型。
/// </summary>
[JsonSerializable(typeof(EchoUserInfo))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.Unspecified,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    UseStringEnumConverter = true,
    AllowOutOfOrderMetadataProperties = true)]
internal partial class TestToolJsonContext : JsonSerializerContext;
