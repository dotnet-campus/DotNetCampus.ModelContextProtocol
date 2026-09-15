using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 提供给源生成器使用，用于序列化 MCP 工具的描述信息。
/// </summary>
[JsonSerializable(typeof(CompiledJsonSchema))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
public partial class CompiledSchemaJsonContext : JsonSerializerContext;
