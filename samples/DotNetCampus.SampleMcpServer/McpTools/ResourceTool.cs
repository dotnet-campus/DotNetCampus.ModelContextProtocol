using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.SampleMcpServer.McpTools;

public class ResourceTool
{
    [McpServerTool]
    public string HandleResource(string resourceUri)
    {
        return resourceUri;
    }

    [McpServerTool]
    public CallToolResult GetResource()
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
