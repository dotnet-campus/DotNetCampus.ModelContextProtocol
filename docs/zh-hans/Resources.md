# Resources

Resources 用于让 MCP 服务器向客户端暴露上下文数据。资源通常是只读的，例如文件内容、配置、数据库结构、图片或运行时状态。

我们假设你在阅读本文前，已经完成了 [快速开始](QuickStart.md) 中 MCP 服务器和客户端的搭建。

## 服务端提供资源

### 初始化

资源需要通过 `WithResources` 注册到 MCP 服务器中：

```csharp
internal class Program
{
    private static async Task Main(string[] args)
    {
        var mcpServer = new McpServerBuilder("示例服务器", "1.0.0")
            .WithResources(r => r
                // 注册各种 MCP 资源
                .WithResource(() => new SampleResources())
            )
            .WithLocalHostHttp(5943, "mcp")
            .Build();

        await mcpServer.RunAsync();
    }
}
```

### MCP 资源方法声明

一个典型的 MCP 资源实现如下：

```csharp
public class SampleResources
{
    /// <summary>
    /// 一个固定 URI 的文本资源。
    /// </summary>
    [McpServerResource(
        UriTemplate = "sample://welcome",
        Name = "Welcome Text",
        Description = "一段欢迎文本")]
    public string WelcomeText()
    {
        return "Hello from MCP Resources.";
    }

    /// <summary>
    /// 一个带 URI 模板参数的 JSON 资源。
    /// </summary>
    /// <param name="context">当前资源读取上下文。</param>
    /// <param name="userId">用户 ID。</param>
    /// <returns>用户资料 JSON。</returns>
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
    /// 一个二进制资源。二进制内容需要用 Base64 编码。
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

在这个示例中：

- `UriTemplate` 是客户端读取资源时使用的 URI 或 URI 模板
- 固定 URI 资源会出现在 `resources/list` 结果中
- 带 `{userId}` 这类模板参数的资源会出现在 `resources/templates/list` 结果中，客户端可按实际 URI 读取
- 如果资源不存在，可以抛出 `McpResourceNotFoundException`

资源方法可以返回以下类型：

- `string`: 文本资源
- `ResourceContents`: 单个资源内容，例如 `TextResourceContents` 或 `BlobResourceContents`
- `IReadOnlyList<ResourceContents>`: 一次返回多个资源内容
- `ReadResourceResult`: 直接控制 MCP 协议层的资源读取结果

如果资源方法需要读取当前请求 URI、`_meta` 元数据，或需要在找不到资源时抛出 `McpResourceNotFoundException`，可在参数中声明 `IMcpServerReadResourceContext`。通过 `context.Meta` 可获取来自客户端请求的元数据（例如分布式追踪的 TraceId）。

## 客户端读取资源

一个典型的 MCP 客户端读取资源的代码如下：

```csharp
var mcpClient = new McpClientBuilder("示例客户端", "1.0.0")
    .WithHttp("http://localhost:5943/mcp")
    .Build();

// 列出固定 URI 资源。
var resources = await mcpClient.ListResourcesAsync();
foreach (var resource in resources.Resources)
{
    Console.WriteLine($"{resource.Uri} {resource.MimeType}");
}

// 读取固定 URI 资源。
var welcome = await mcpClient.ReadResourceAsync("sample://welcome");
Console.WriteLine(welcome.Contents.OfType<TextResourceContents>().FirstOrDefault()?.Text);

// 读取模板 URI 资源。
var profile = await mcpClient.ReadResourceAsync("sample://users/42/profile");
Console.WriteLine(profile.Contents.OfType<TextResourceContents>().FirstOrDefault()?.Text);

// 读取二进制资源。
var binary = await mcpClient.ReadResourceAsync("sample://hello.bin");
var blob = binary.Contents.OfType<BlobResourceContents>().FirstOrDefault();
if (blob is not null)
{
    var bytes = Convert.FromBase64String(blob.Blob);
    Console.WriteLine($"Blob bytes: {bytes.Length}");
}
```

如果你需要统一处理所有资源内容，可以按资源内容类型分发：

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

> 在智能体程序中，通常需要同时管理多个 MCP 服务器。完整的 MCP 服务器管理器示例请参阅 [McpServerManager](McpServerManager.md)。
