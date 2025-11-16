using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.SampleMcpServer.McpTools;

public class ResourceTool
{
    /// <summary>
    /// 测试按 URI 处理资源
    /// </summary>
    /// <param name="resourceUri"></param>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public string TestHandleResource(string resourceUri)
    {
        return resourceUri;
    }

    /// <summary>
    /// 测试获取嵌入的资源文件
    /// </summary>
    /// <returns></returns>
    [McpServerTool(ReadOnly = true)]
    public CallToolResult TestGetResource()
    {
        var image = File.ReadAllBytes(Path.Join(AppContext.BaseDirectory, "mcp-simple-diagram.avif"));
        return new CallToolResult
        {
            Content =
            [
                new EmbeddedResourceContentBlock
                {
                    Resource = new BlobResourceContents
                    {
                        Uri = "resource://mcp-simple-diagram.avif",
                        MimeType = "image/avif",
                        Blob = Convert.ToBase64String(image),
                    },
                },
            ],
        };
    }
}
