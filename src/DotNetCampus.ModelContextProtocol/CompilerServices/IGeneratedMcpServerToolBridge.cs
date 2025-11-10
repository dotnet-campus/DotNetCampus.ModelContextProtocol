using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Protocol;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 源生成器实现此接口，桥接 MCP 服务器工具请求的处理和具体工具方法的调用。
/// </summary>
public interface IGeneratedMcpServerToolBridge
{
    /// <summary>
    /// 调用 MCP 服务器工具的方法。
    /// </summary>
    /// <param name="jsonArguments">来自 MCP 协议中 tools/call 请求中 arguments 字段的 JSON 元素。</param>
    /// <param name="jsonSerializerContext">用于反序列化参数和序列化结果的 JSON 序列化上下文。</param>
    /// <returns>表示工具调用结果的 JSON 元素。</returns>
    ValueTask<CallToolResult> CallTool(JsonElement jsonArguments, JsonSerializerContext jsonSerializerContext);
}
