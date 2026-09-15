# Resources

Resources allow an MCP server to expose contextual data to clients. Resources are typically read-only, such as file contents, configuration, database schemas, images, or runtime state.

We assume you have already completed the MCP server and client setup described in [Quick Start](QuickStart.md) before reading this guide.

## Server-Side Resource Provision

### Initialization

Resources must be registered with the MCP server via `WithResources`:

```csharp
internal class Program
{
    private static async Task Main(string[] args)
    {
        var mcpServer = new McpServerBuilder("Example Server", "1.0.0")
            .WithResources(r => r
                // Register various MCP resources
                .WithResource(() => new SampleResources())
            )
            .WithLocalHostHttp(5943, "mcp")
            .Build();

        await mcpServer.RunAsync();
    }
}
```

### MCP Resource Method Declaration

A typical MCP resource implementation looks like this:

```csharp
public class SampleResources
{
    /// <summary>
    /// A text resource with a fixed URI.
    /// </summary>
    [McpServerResource(
        UriTemplate = "sample://welcome",
        Name = "Welcome Text",
        Description = "A welcome text message")]
    public string WelcomeText()
    {
        return "Hello from MCP Resources.";
    }

    /// <summary>
    /// A JSON resource with a URI template parameter.
    /// </summary>
    /// <param name="context">The current resource read context.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>A user profile in JSON.</returns>
    [McpServerResource(
        UriTemplate = "sample://users/{userId}/profile",
        Name = "User Profile",
        MimeType = "application/json")]
    public TextResourceContents UserProfile(IMcpServerReadResourceContext context, int userId)
    {
        if (userId <= 0)
        {
            throw new McpResourceNotFoundException(context);
        }

        return new TextResourceContents
        {
            Uri = context.Uri,
            MimeType = "application/json",
            Text = $$"""{"userId":{{userId}},"name":"User {{userId}}"}"""
        };
    }

    /// <summary>
    /// A binary resource. Binary content must be Base64-encoded.
    /// </summary>
    [McpServerResource(
        UriTemplate = "sample://hello.bin",
        Name = "Hello Binary",
        MimeType = "application/octet-stream")]
    public BlobResourceContents HelloBinary()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("Hello from binary resource.");
        return new BlobResourceContents
        {
            Uri = "sample://hello.bin",
            MimeType = "application/octet-stream",
            Blob = Convert.ToBase64String(bytes)
        };
    }
}
```

In this example:

- `UriTemplate` is the URI or URI template used by clients when reading the resource
- Fixed URI resources appear in the `resources/list` result
- Resources with template parameters like `{userId}` appear in the `resources/templates/list` result, and clients can read them using the actual URI
- If a resource does not exist, you can throw `McpResourceNotFoundException`

Resource methods can return the following types:

- `string`: A text resource
- `ResourceContents`: A single resource content, such as `TextResourceContents` or `BlobResourceContents`
- `IReadOnlyList<ResourceContents>`: Multiple resource contents returned at once
- `ReadResourceResult`: Direct control over the resource read result at the MCP protocol layer

If a resource method needs to read the current request URI, `_meta` metadata, or needs to throw `McpResourceNotFoundException` when a resource is not found, declare `IMcpServerReadResourceContext` as a parameter. Metadata from the client request (e.g. TraceId for distributed tracing) can be accessed via `context.Meta`.

Similar to tool methods, resource methods can also access transport session information (such as SessionId, client name/version, etc.) via `context.TransportSession`, and HTTP transport-specific information (such as request headers) via `context.HttpTransportContext`. See [context details in the Tools documentation](Tools.md#imcpservercalltoolcontext) for more.

## Client-Side Resource Reading

Typical code for an MCP client reading resources:

```csharp
var mcpClient = new McpClientBuilder("Example Client", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

// List fixed URI resources.
var resources = await mcpClient.ListResourcesAsync();
foreach (var resource in resources.Resources)
{
    Console.WriteLine($"{resource.Uri} {resource.MimeType}");
}

// Read a fixed URI resource.
var welcome = await mcpClient.ReadResourceAsync("sample://welcome");
Console.WriteLine(welcome.Contents.OfType<TextResourceContents>().FirstOrDefault()?.Text);

// Read a template URI resource.
var profile = await mcpClient.ReadResourceAsync("sample://users/42/profile");
Console.WriteLine(profile.Contents.OfType<TextResourceContents>().FirstOrDefault()?.Text);

// Read a binary resource.
var binary = await mcpClient.ReadResourceAsync("sample://hello.bin");
var blob = binary.Contents.OfType<BlobResourceContents>().FirstOrDefault();
if (blob is not null)
{
    var bytes = Convert.FromBase64String(blob.Blob);
    Console.WriteLine($"Blob bytes: {bytes.Length}");
}
```

If you need to handle all resource contents uniformly, dispatch by resource content type:

```csharp
var result = await mcpClient.ReadResourceAsync("sample://users/42/profile");
foreach (var content in result.Contents)
{
    switch (content)
    {
        case TextResourceContents text:
            Console.WriteLine(text.Text);
            break;

        case BlobResourceContents blob:
            var bytes = Convert.FromBase64String(blob.Blob);
            Console.WriteLine($"Blob bytes: {bytes.Length}");
            break;
    }
}
```

> In agent programs, you typically need to manage multiple MCP servers simultaneously. For a complete MCP server manager example, see [McpServerManager](McpServerManager.md).
