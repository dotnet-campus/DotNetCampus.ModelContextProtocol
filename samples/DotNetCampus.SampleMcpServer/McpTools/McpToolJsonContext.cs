using System.Text.Json.Serialization;

namespace DotNetCampus.SampleMcpServer.McpTools;

[JsonSerializable(typeof(EchoExtraData))]
[JsonSerializable(typeof(EchoOptions))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.Unspecified,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    UseStringEnumConverter = true)]
internal partial class McpToolJsonContext : JsonSerializerContext;
