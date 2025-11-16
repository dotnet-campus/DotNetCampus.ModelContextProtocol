using System.Text.Json.Serialization;

namespace DotNetCampus.SampleMcpServer.McpTools;

[JsonSerializable(typeof(EchoExtraData))]
[JsonSerializable(typeof(EchoOptions))]
[JsonSerializable(typeof(LocalTimeInfo))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.Unspecified,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    UseStringEnumConverter = true,
    AllowOutOfOrderMetadataProperties = true)]
internal partial class McpToolJsonContext : JsonSerializerContext;
