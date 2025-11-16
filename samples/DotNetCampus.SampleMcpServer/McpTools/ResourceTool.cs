using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.SampleMcpServer.McpTools;

public class ResourceTool
{
    [McpServerTool]
    public CallToolResult GetEmbeddedResourceContent()
    {
        return new CallToolResult
        {
            Content =
            [
                new EmbeddedResourceContentBlock
                {
                    Resource = new TextResourceContents
                    {
                        Uri = "resource://DotNetCampus.SampleMcpServer.Resources.SampleTextResource.txt",
                        Text = """
这是一个嵌入的文本资源示例。
它包含多行文本内容，用于演示嵌入资源的功能。
你可以在 MCP 客户端中访问和使用这个资源。
""",
                        MimeType = "text/plain",
                    },
                },
            ],
        };
    }
}
