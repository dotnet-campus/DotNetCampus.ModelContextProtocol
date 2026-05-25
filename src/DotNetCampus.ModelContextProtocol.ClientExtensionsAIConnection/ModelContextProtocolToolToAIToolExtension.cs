using System.Collections.ObjectModel;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using Microsoft.Extensions.AI;

namespace DotNetCampus.ModelContextProtocol.ClientExtensionsAIConnection;

public static class ModelContextProtocolToolToAIToolExtension
{
    /// <summary>
    /// 列出 MCP 服务端提供的 AI 工具。
    /// </summary>
    /// <param name="mcpClient">MCP 客户端。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可直接用于 AI 调用的工具集合。</returns>
    public static async Task<IReadOnlyList<AITool>> ListAIToolsAsync(this McpClient mcpClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mcpClient);

        ListToolsResult listToolsResult = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var toolList = listToolsResult.Tools;
        var resultList = new List<AITool>(toolList.Count);
        foreach (Tool tool in toolList)
        {
            resultList.Add(new ModelContextProtocolAITool(mcpClient, tool));
        }

        return resultList;
    }
}

public sealed class ModelContextProtocolAITool : AIFunction
{
    /// <summary>
    /// 初始化 MCP AI 工具适配实例。
    /// </summary>
    /// <param name="mcpClient">MCP 客户端。</param>
    /// <param name="tool">MCP 工具定义。</param>
    public ModelContextProtocolAITool(McpClient mcpClient, Tool tool)
    {
        ArgumentNullException.ThrowIfNull(mcpClient);
        ArgumentNullException.ThrowIfNull(tool);

        _mcpClient = mcpClient;
        Name = tool.Name;
        Description = tool.Description ?? string.Empty;
        JsonSchema = tool.InputSchema;
        ReturnJsonSchema = tool.OutputSchema;
    }

    private readonly McpClient _mcpClient;

    public override JsonElement JsonSchema { get; }
    public override JsonElement? ReturnJsonSchema { get; }
    public override string Name { get; }
    public override string Description { get; }

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        JsonElement? jsonArguments = arguments.Count == 0
            ? null
            : JsonSerializer.SerializeToElement((IReadOnlyDictionary<string, object?>)arguments);

        CallToolResult result = await _mcpClient.CallToolAsync(Name, jsonArguments, cancellationToken)
            .ConfigureAwait(false);

        if (result.StructuredContent is { } structuredContent)
        {
            return structuredContent;
        }

        return result.ToString();
    }
}