using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.SampleMcpServer.McpResources;

public class SampleResource
{
    [McpServerResource(UriTemplate = "test://direct/text/resource", Name = "Direct Text Resource", MimeType = "text/plain")]
    public string DirectTextResource()
    {
        return "This is a direct resource";
    }

    /// <summary>
    /// A template resource with a numeric ID
    /// </summary>
    /// <param name="context"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    [McpServerResource(UriTemplate = "test://template/resource/{id}", Name = "Template Resource")]
    public ResourceContents TemplateResource(IMcpServerReadResourceContext context, int id)
    {
        int index = id - 1;
        if ((uint)index >= ResourceGenerator.Resources.Count)
        {
            throw new McpResourceNotFoundException(context);
        }

        var resource = ResourceGenerator.Resources[index];
        return resource.MimeType == "text/plain"
            ? new TextResourceContents
            {
                Text = resource.Description!,
                MimeType = resource.MimeType,
                Uri = resource.Uri,
            }
            : new BlobResourceContents
            {
                Blob = resource.Description!,
                MimeType = resource.MimeType,
                Uri = resource.Uri,
            };
    }

    /// <summary>
    /// A multi-parameter template resource
    /// </summary>
    /// <param name="context"></param>
    /// <param name="id0">First ID parameter</param>
    /// <param name="id1">Second ID parameter</param>
    /// <returns></returns>
    [McpServerResource(UriTemplate = "test://template/resource/{id1}/foo/{id0}/bar", Name = "Multi-Parameter Resource")]
    public IReadOnlyList<ResourceContents> MultiParameterResource(IMcpServerReadResourceContext context, int id0, int id1)
    {
        return
        [
            new TextResourceContents
            {
                Text = $"Multi-parameter resource: id0={id0}, id1={id1}",
                MimeType = "text/plain",
                Uri = context.Uri,
            },
            new TextResourceContents
            {
                Text = $"Parameters received: id0={id0}, id1={id1}",
                MimeType = "text/plain",
                Uri = context.Uri,
            },
        ];
    }
}

internal static class ResourceGenerator
{
    public static List<Resource> Resources { get; } = Enumerable.Range(1, 100).Select(i =>
    {
        var uri = $"test://template/resource/{i}";
        if (i % 2 != 0)
        {
            return new Resource
            {
                Uri = uri,
                Name = $"Resource {i}",
                MimeType = "text/plain",
                Description = $"Resource {i}: This is a plaintext resource",
            };
        }
        else
        {
            var buffer = System.Text.Encoding.UTF8.GetBytes($"Resource {i}: This is a base64 blob");
            return new Resource
            {
                Uri = uri,
                Name = $"Resource {i}",
                MimeType = "application/octet-stream",
                Description = Convert.ToBase64String(buffer),
            };
        }
    }).ToList();
}
