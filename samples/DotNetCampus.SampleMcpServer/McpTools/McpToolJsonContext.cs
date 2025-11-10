using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Protocol;

namespace DotNetCampus.SampleMcpServer.McpTools;

[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(CallToolResult))]
[JsonSerializable(typeof(EchoExtraData))]
[JsonSerializable(typeof(EchoOptions))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.Unspecified,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    UseStringEnumConverter = true)]
internal partial class McpToolJsonContext : JsonSerializerContext;
