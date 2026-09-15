# Resources

Resources 用於讓 MCP 伺服器向用戶端暴露內容資料。資源通常是唯讀的，例如檔案內容、組態、資料庫結構、圖片或執行時期狀態。

我們假設你在閱讀本文前，已經完成了 [快速開始](QuickStart.md) 中 MCP 伺服器和用戶端的建置。

## 伺服器端提供資源

### 初始化

資源需要透過 `WithResources` 註冊到 MCP 伺服器中：

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

### MCP 資源方法宣告

一個典型的 MCP 資源實作如下：

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

在這個範例中：

- `UriTemplate` 是用戶端讀取資源時使用的 URI 或 URI 範本
- 固定 URI 資源會出現在 `resources/list` 結果中
- 帶 `{userId}` 這類範本參數的資源會出現在 `resources/templates/list` 結果中，用戶端可按實際 URI 讀取
- 如果資源不存在，可以擲出 `McpResourceNotFoundException`

資源方法可以傳回以下型別：

- `string`: 文字資源
- `ResourceContents`: 單一資源內容，例如 `TextResourceContents` 或 `BlobResourceContents`
- `IReadOnlyList<ResourceContents>`: 一次傳回多個資源內容
- `ReadResourceResult`: 直接控制 MCP 協定層的資源讀取結果

如果資源方法需要讀取目前要求 URI、`_meta` 中繼資料，或需要在找不到資源時擲出 `McpResourceNotFoundException`，可在參數中宣告 `IMcpServerReadResourceContext`。透過 `context.Meta` 可取得來用戶端要求的中繼資料（例如分散式追蹤的 TraceId）。

與工具方法類似，資源方法也可透過 `context.TransportSession` 取得傳輸層會話資訊（如 SessionId、用戶端名稱/版本等），透過 `context.HttpTransportContext` 取得 HTTP 傳輸層專屬資訊（如要求標頭）。詳見 [Tools 文件中的內容說明](Tools.md#imcpservercalltoolcontext-內容)。

## 用戶端讀取資源

一個典型的 MCP 用戶端讀取資源的程式碼如下：

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

如果你需要統一處理所有資源內容，可以按資源內容型別分派：

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

> 在智慧型代理人程式中，通常需要同時管理多個 MCP 伺服器。完整的 MCP 伺服器管理器範例請參閱 [McpServerManager](McpServerManager.md)。
