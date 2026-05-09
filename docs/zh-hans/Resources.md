# Resources

Resources 用于让服务端向客户端暴露上下文数据。资源通常是只读的，例如文件内容、配置、数据库结构、图片或运行时状态。

## 服务端提供资源

```csharp
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

## 客户端读取资源

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

资源方法可直接返回 `string`、`ResourceContents`、`ReadResourceResult` 或资源内容列表。需要访问请求 URI 时，在参数中声明 `IMcpServerReadResourceContext`。
