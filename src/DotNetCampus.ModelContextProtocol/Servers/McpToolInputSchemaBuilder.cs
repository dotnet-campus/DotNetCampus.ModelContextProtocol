using System.Text.Json;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 用于构建 MCP 工具的 inputSchema（JSON Schema 格式）。
/// </summary>
public static class McpToolInputSchemaBuilder
{
    /// <summary>
    /// 创建一个空的 inputSchema（无参数的工具）。
    /// </summary>
    /// <returns>符合 JSON Schema 规范的空对象 schema。</returns>
    public static JsonElement CreateEmptySchema()
    {
        using var doc = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// 根据参数信息创建 inputSchema。
    /// </summary>
    /// <param name="parameters">参数信息列表。</param>
    /// <returns>符合 JSON Schema 规范的 schema。</returns>
    public static JsonElement CreateSchema(IEnumerable<ToolParameterInfo> parameters)
    {
        var paramList = parameters.ToList();
        if (paramList.Count == 0)
        {
            return CreateEmptySchema();
        }

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("type", "object");

        // 写入 properties
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        foreach (var param in paramList)
        {
            writer.WritePropertyName(param.JsonName);
            WritePropertySchema(writer, param);
        }
        writer.WriteEndObject();

        // 写入 required
        writer.WritePropertyName("required");
        writer.WriteStartArray();
        foreach (var param in paramList.Where(p => p.IsRequired))
        {
            writer.WriteStringValue(param.JsonName);
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.Flush();

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.Clone();
    }

    private static void WritePropertySchema(Utf8JsonWriter writer, ToolParameterInfo parameter)
    {
        writer.WriteStartObject();
        writer.WriteString("type", GetJsonSchemaType(parameter.TypeName));

        if (!string.IsNullOrWhiteSpace(parameter.Description))
        {
            writer.WriteString("description", parameter.Description);
        }

        writer.WriteEndObject();
    }

    private static string GetJsonSchemaType(string typeName)
    {
        // 移除可空标记和命名空间前缀
        var cleanType = typeName
            .Replace("?", "")
            .Replace("global::", "")
            .Trim();

        // 基本类型映射
        return cleanType switch
        {
            "System.String" or "string" => "string",
            "System.Int32" or "int" => "integer",
            "System.Int64" or "long" => "integer",
            "System.Int16" or "short" => "integer",
            "System.Byte" or "byte" => "integer",
            "System.Double" or "double" => "number",
            "System.Single" or "float" => "number",
            "System.Decimal" or "decimal" => "number",
            "System.Boolean" or "bool" => "boolean",
            _ when cleanType.StartsWith("System.Collections.Generic.IEnumerable<") => "array",
            _ when cleanType.StartsWith("System.Collections.Generic.List<") => "array",
            _ when cleanType.EndsWith("[]") => "array",
            _ => "object", // 默认为 object（包括枚举、自定义类型等）
        };
    }
}

/// <summary>
/// 工具参数信息。
/// </summary>
public sealed record ToolParameterInfo
{
    /// <summary>
    /// 参数在 JSON 中的名称（kebab-case）。
    /// </summary>
    public required string JsonName { get; init; }

    /// <summary>
    /// 参数类型的完整名称。
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// 参数是否必需（无默认值）。
    /// </summary>
    public required bool IsRequired { get; init; }

    /// <summary>
    /// 参数描述（从 XML 文档注释提取）。
    /// </summary>
    public string? Description { get; init; }
}
