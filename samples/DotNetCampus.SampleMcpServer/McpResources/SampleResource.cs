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
    public static ResourceContents TemplateResource(IMcpServerReadResourceContext context, int id)
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
    public static ResourceContents MultiParameterResource(IMcpServerReadResourceContext context, int id0, int id1)
    {
        return new TextResourceContents
        {
            Text = $"Multi-parameter resource: id0={id0}, id1={id1}",
            MimeType = "text/plain",
            Uri = context.Uri,
        };
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

public sealed class SampleResource_DirectTextResource_Bridge(Func<SampleResource> targetFactory) : IMcpServerResource
{
    private SampleResource Target => targetFactory();

    /// <inheritdoc />
    public string ResourceName { get; } = "Direct Text Resource";

    /// <inheritdoc />
    public string UriTemplate { get; } = "test://direct/text/resource";

    /// <inheritdoc />
    public bool IsTemplate => false;

    /// <inheritdoc />
    public object GetResourceDefinition(DotNetCampus.ModelContextProtocol.CompilerServices.CompiledSchemaJsonContext jsonContext)
    {
        return new Resource
        {
            Name = ResourceName,
            Uri = UriTemplate,
            MimeType = "text/plain",
        };
    }

    /// <inheritdoc />
    public ValueTask<ReadResourceResult> ReadResource(IMcpServerReadResourceContext context)
    {
        var result = Target.DirectTextResource();
        return ValueTask.FromResult(ReadResourceResult.FromResult(new TextResourceContents
        {
            Uri = UriTemplate,
            MimeType = "text/plain",
            Text = result,
        }));
    }
}

public sealed class SampleResource_TemplateResource_Bridge(Func<SampleResource> targetFactory) : IMcpServerResource
{
    private SampleResource Target => targetFactory();

    /// <inheritdoc />
    public string ResourceName { get; } = "Template Resource";

    /// <inheritdoc />
    public string UriTemplate { get; } = "test://template/resource/{id}";

    /// <inheritdoc />
    public bool IsTemplate => true;

    /// <inheritdoc />
    public object GetResourceDefinition(DotNetCampus.ModelContextProtocol.CompilerServices.CompiledSchemaJsonContext jsonContext)
    {
        return new ResourceTemplate
        {
            Name = ResourceName,
            UriTemplate = UriTemplate,
            Description = "A template resource with a numeric ID",
        };
    }

    /// <inheritdoc />
    public ValueTask<ReadResourceResult> ReadResource(IMcpServerReadResourceContext context)
    {
        // 编译期确定的 URI 模板: "test://template/resource/{id}"
        // URI 段列表（编译期生成）:
        //   [0] = "test://template/resource/" (静态)
        //   [1] = {id} (参数，int)
        
        var uri = context.Uri;
        
        // 段 0: 验证静态前缀 "test://template/resource/"
        const int segment0Length = 26;
        if (uri.Length <= segment0Length || !uri.AsSpan(0, segment0Length).SequenceEqual("test://template/resource/"))
        {
            throw new McpResourceNotFoundException(context);
        }

        // 段 1: 提取参数 {id} (int)
        var idSpan = uri.AsSpan(segment0Length);
        if (!int.TryParse(idSpan, out var id))
        {
            throw new McpResourceNotFoundException(context);
        }

        // 调用目标方法
        var result = SampleResource.TemplateResource(context, id);
        
        // 返回结果
        return ValueTask.FromResult(ReadResourceResult.FromResult(result));
    }
}

public sealed class SampleResource_MultiParameterResource_Bridge(Func<SampleResource> targetFactory) : IMcpServerResource
{
    private SampleResource Target => targetFactory();

    /// <inheritdoc />
    public string ResourceName { get; } = "Multi-Parameter Resource";

    /// <inheritdoc />
    public string UriTemplate { get; } = "test://template/resource/{id1}/foo/{id0}/bar";

    /// <inheritdoc />
    public bool IsTemplate => true;

    /// <inheritdoc />
    public object GetResourceDefinition(DotNetCampus.ModelContextProtocol.CompilerServices.CompiledSchemaJsonContext jsonContext)
    {
        return new ResourceTemplate
        {
            Name = ResourceName,
            UriTemplate = UriTemplate,
            Description = "A multi-parameter template resource",
        };
    }

    /// <inheritdoc />
    public ValueTask<ReadResourceResult> ReadResource(IMcpServerReadResourceContext context)
    {
        // 编译期确定的 URI 模板: "test://template/resource/{id1}/foo/{id0}/bar"
        // URI 段列表（编译期生成）:
        //   [0] = "test://template/resource/" (静态)
        //   [1] = {id1} (参数，int)
        //   [2] = "/foo/" (静态)
        //   [3] = {id0} (参数，int)
        //   [4] = "/bar" (静态)
        
        var uri = context.Uri;
        var span = uri.AsSpan();
        
        // 段 0: 验证静态前缀 "test://template/resource/"
        const int segment0Length = 26;
        if (span.Length <= segment0Length || !span.Slice(0, segment0Length).SequenceEqual("test://template/resource/"))
        {
            throw new McpResourceNotFoundException(context);
        }
        var position = segment0Length;

        // 段 1: 提取参数 {id1} (int)
        var segment1End = span.Slice(position).IndexOf('/');
        if (segment1End < 0)
        {
            throw new McpResourceNotFoundException(context);
        }
        var id1Span = span.Slice(position, segment1End);
        if (!int.TryParse(id1Span, out var id1))
        {
            throw new McpResourceNotFoundException(context);
        }
        position += segment1End;

        // 段 2: 验证静态段 "/foo/"
        const string segment2 = "/foo/";
        const int segment2Length = 5;
        if (span.Length <= position + segment2Length || !span.Slice(position, segment2Length).SequenceEqual(segment2))
        {
            throw new McpResourceNotFoundException(context);
        }
        position += segment2Length;

        // 段 3: 提取参数 {id0} (int)
        var segment3End = span.Slice(position).IndexOf('/');
        if (segment3End < 0)
        {
            throw new McpResourceNotFoundException(context);
        }
        var id0Span = span.Slice(position, segment3End);
        if (!int.TryParse(id0Span, out var id0))
        {
            throw new McpResourceNotFoundException(context);
        }
        position += segment3End;

        // 段 4: 验证静态后缀 "/bar"
        const string segment4 = "/bar";
        if (!span.Slice(position).SequenceEqual(segment4))
        {
            throw new McpResourceNotFoundException(context);
        }

        // 调用目标方法（注意参数顺序与 URI 模板中的顺序不同）
        var result = SampleResource.MultiParameterResource(context, id0, id1);
        
        // 返回结果
        return ValueTask.FromResult(ReadResourceResult.FromResult(result));
    }
}
