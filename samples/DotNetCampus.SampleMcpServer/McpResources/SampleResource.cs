using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.SampleMcpServer.McpResources;

public class SampleResource
{
    [McpServerResource(UriTemplate = "test://direct/text/resource", Name = "Direct Text Resource", MimeType = "text/plain")]
    public static string DirectTextResource()
    {
        return "This is a direct resource";
    }
}
