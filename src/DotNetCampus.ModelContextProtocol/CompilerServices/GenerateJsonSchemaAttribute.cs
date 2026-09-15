using System.Diagnostics;
using System.Text.Json.Serialization;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 标记在一个 <see cref="JsonSerializerContext"/> 的派生类上，为该类型上所有标注了 <see cref="JsonSerializableAttribute"/> 的类型在编译期生成 Json Schema。
/// 所有生成的 JsonSchema 与 MCP 工具上所生成的 Json Schema 同源，同效果。
/// <para/>
/// 示例用法：
/// <code>
/// var jsonSchema = SampleJsonSerializerContext.Default.Foo.GetCompilerGeneratedJsonSchema();
/// </code>
/// <code>
/// [JsonSerializable(typeof(Foo))]
/// [JsonSerializable(typeof(Bar))]
/// [JsonSourceGenerationOptions(
///     PropertyNameCaseInsensitive = true,
///     PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
///     NumberHandling = JsonNumberHandling.AllowReadingFromString,
///     UseStringEnumConverter = true)]
/// [DotNetCampus.ModelContextProtocol.CompilerServices.GenerateJsonSchema]
/// internal partial class SampleJsonSerializerContext : JsonSerializerContext;
/// </code>
/// </summary>
[Conditional("FOR_SOURCE_GENERATION_ONLY")]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class GenerateJsonSchemaAttribute : Attribute
{
}
