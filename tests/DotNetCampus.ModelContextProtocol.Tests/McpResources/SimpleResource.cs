using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.Tests.McpResources;

/// <summary>
/// 测试用的简单资源提供者。
/// </summary>
public class SimpleResource
{
    /// <summary>
    /// 简单文本资源。
    /// </summary>
    [McpServerResource(UriTemplate = "test://file1", Name = "Test File 1", Description = "A simple text resource for testing")]
    public string TextFile()
    {
        return "Hello, this is test file 1 content.";
    }

    /// <summary>
    /// 带参数的模板资源。
    /// </summary>
    [McpServerResource(UriTemplate = "test://users/{userId}/profile", Name = "User Profile")]
    public ResourceContents UserProfile(IMcpServerReadResourceContext context, int userId)
    {
        if (userId <= 0)
        {
            throw new McpResourceNotFoundException(context);
        }

        return new TextResourceContents
        {
            Uri = $"test://users/{userId}/profile",
            MimeType = "application/json",
            Text = $$"""{"userId": {{userId}}, "name": "User{{userId}}", "email": "user{{userId}}@test.com"}"""
        };
    }

    /// <summary>
    /// 二进制资源（Base64 编码的图片模拟）。
    /// </summary>
    [McpServerResource(UriTemplate = "test://image.png", Name = "Test Image", MimeType = "image/png")]
    public BlobResourceContents BinaryImage()
    {
        // 1x1 透明 PNG 的 Base64 编码
        const string pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        return new BlobResourceContents
        {
            Uri = "test://image.png",
            MimeType = "image/png",
            Blob = pngBase64
        };
    }
}
