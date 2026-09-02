using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.Tests.McpTools;

/// <summary>
/// 用于测试工具的 JSON 序列化上下文。
/// 包含所有测试工具使用的复杂类型。
/// </summary>
[JsonSerializable(typeof(EchoUserInfo))]
[JsonSerializable(typeof(IReadOnlyList<SchemaContractEnum>))]
[JsonSerializable(typeof(SchemaContractInput))]
[JsonSerializable(typeof(SchemaContractOutput))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(SchemaContractPolymorphicBase))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    UseStringEnumConverter = true,
    AllowOutOfOrderMetadataProperties = true)]
internal partial class TestToolJsonContext : JsonSerializerContext;
