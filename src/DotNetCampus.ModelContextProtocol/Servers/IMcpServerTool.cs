using System.Text.Json.Serialization;
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
    /// 获取工具在指定业务 JSON 序列化上下文下的定义信息。
    /// </summary>
    /// <param name="jsonSerializerContext">为此 MCP 工具提供 Json 序列化所需的上下文。</param>
    /// <returns>工具的定义信息。</returns>
    Tool GetToolDefinition(JsonSerializerContext jsonSerializerContext);

    /// <summary>
    /// 调用 MCP 服务器工具的方法。
    /// </summary>
    /// <param name="context">调用工具时的上下文信息。</param>
    /// <returns>表示工具调用结果的 JSON 元素。</returns>
    ValueTask<CallToolResult> CallTool(IMcpServerCallToolContext context);
}
