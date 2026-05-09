# Resources

Resources let a server expose context data to a client. A resource is usually read-only, such as file content, configuration, database schema, images, or runtime state.

## Providing resources from the server

```csharp
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

var mcpServer = new McpServerBuilder("ResourceServer", "1.0.0")
    .WithResources(resources => resources.WithResource(() => new AppResources()))
    .WithLocalHostHttp(5943, "mcp")
    .Build();

await mcpServer.RunAsync();

public class AppResources
{
    [McpServerResource(
        UriTemplate = "docs://welcome",
        Name = "Welcome",
        Description = "A small text resource")]
    public TextResourceContents Welcome()
    {
        return new TextResourceContents
        {
            Uri = "docs://welcome",
            MimeType = "text/plain",
            Text = "Hello from MCP Resources."
        };
    }

    [McpServerResource(
        UriTemplate = "users://{userId}/profile",
        Name = "User Profile",
        MimeType = "application/json")]
    public TextResourceContents UserProfile(IMcpServerReadResourceContext context, int userId)
    {
        return new TextResourceContents
        {
            Uri = context.Uri,
            MimeType = "application/json",
            Text = $$"""{"userId":{{userId}},"name":"User {{userId}}"}"""
        };
    }
}
```

## Reading resources from the client

```csharp
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;

await using var mcpClient = new McpClientBuilder()
    .WithClientInfo("ResourceClient", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

var resources = await mcpClient.ListResourcesAsync();
foreach (var resource in resources.Resources)
{
    Console.WriteLine($"{resource.Uri} {resource.MimeType}");
}

var result = await mcpClient.ReadResourceAsync("users://42/profile");
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

A resource method can return `string`, `ResourceContents`, `ReadResourceResult`, or a list of resource contents. To access the requested URI, declare an `IMcpServerReadResourceContext` parameter.
