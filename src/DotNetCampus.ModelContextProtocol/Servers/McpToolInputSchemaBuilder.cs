using System.Text.Json;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 用于构建 MCP 工具的 inputSchema（JSON Schema 格式）。
/// </summary>
/// <remarks>
/// 此类仅用于在源生成器生成的代码中使用，所有 Schema 都应在编译时生成为 JSON 字符串常量。
/// </remarks>
public static class McpToolInputSchemaBuilder
{
    /// <summary>
    /// 从预生成的 JSON 字符串创建 inputSchema。
    /// </summary>
    /// <param name="jsonSchema">编译时生成的 JSON Schema 字符串。</param>
    /// <returns>符合 JSON Schema 规范的 JsonElement。</returns>
    /// <remarks>
    /// 此方法支持 AOT，因为 JSON 字符串在编译时已经生成好，无需运行时反射或类型解析。
    /// </remarks>
    public static JsonElement CreateSchema(string jsonSchema)
    {
        using var doc = JsonDocument.Parse(jsonSchema);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// 创建一个空的 inputSchema（无参数的工具）。
    /// </summary>
    /// <returns>符合 JSON Schema 规范的空对象 schema。</returns>
    public static JsonElement CreateEmptySchema()
    {
        return CreateSchema("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """);
    }
}
