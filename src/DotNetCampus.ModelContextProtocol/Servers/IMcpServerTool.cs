using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 表示 MCP 服务器工具的接口。
/// </summary>
public interface IMcpServerTool
{
    /// <summary>
    /// 获取工具在 MCP 协议中的名称。
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    /// 获取工具的定义信息，这些信息将被 AI 查看，以了解工具的功能和使用方法。
    /// </summary>
    /// <returns>工具的定义信息。</returns>
    Tool GetToolDefinition(InputSchemaJsonObjectJsonContext jsonContext);

    /// <summary>
    /// 调用 MCP 服务器工具的方法。
    /// </summary>
    /// <param name="jsonArguments">来自 MCP 协议中 tools/call 请求中 arguments 字段的 JSON 元素。</param>
    /// <param name="jsonSerializerContext">用于反序列化参数和序列化结果的 JSON 序列化上下文。</param>
    /// <param name="cancellationToken">用于取消操作的取消令牌。</param>
    /// <returns>表示工具调用结果的 JSON 元素。</returns>
    ValueTask<CallToolResult> CallTool(JsonElement jsonArguments, JsonSerializerContext jsonSerializerContext, CancellationToken cancellationToken);
}
