using System.Net;
using System.Text;
using System.Text.Json;

namespace DotNetCampus.ModelContextProtocol.Tests.Transports;

/// <summary>
/// HTTP 传输层特性测试：SSE 和 POST 通信模式的特定测试。
/// </summary>
[TestClass]
public class HttpTransportTests
{
    [TestMethod("Post_NoSessionId: 旧协议下不带 sessionId 应返回错误")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task Post_NoSessionId(HttpTransportType type)
    {
        await using var package = await TestMcpFactory.Shared.CreateLegacyHttpAsync(type);
        using var client = CreateHttpClient();

        using var response = await client.PostAsync(
            new Uri($"{package.Endpoint}/messages", UriKind.Absolute),
            CreateInitializeRequestContent());

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod("Sse_EndpointEvent: 旧协议 SSE 连接应首先收到 endpoint 事件")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task Sse_EndpointEvent(HttpTransportType type)
    {
        await using var package = await TestMcpFactory.Shared.CreateLegacyHttpAsync(type);
        using var client = CreateHttpClient();
        using var sseRequest = new HttpRequestMessage(HttpMethod.Get, new Uri($"{package.Endpoint}/sse", UriKind.Absolute));
        sseRequest.Headers.Accept.ParseAdd("text/event-stream");

        using var sseResponse = await client.SendAsync(sseRequest, HttpCompletionOption.ResponseHeadersRead);
        Assert.AreEqual(HttpStatusCode.OK, sseResponse.StatusCode);
        Assert.IsTrue(sseResponse.Content.Headers.ContentType?.MediaType?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true);

        using var sseStream = await sseResponse.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(sseStream, Encoding.UTF8, leaveOpen: true);
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var endpointEvent = await ReadNextSseEventAsync(reader, timeoutCts.Token);
        Assert.AreEqual("endpoint", endpointEvent.EventName);
        StringAssert.Contains(endpointEvent.Data, "/mcp/messages?sessionId=");

        var messageEndpoint = ResolveEndpoint(package.Endpoint, endpointEvent.Data);
        using var initializeResponse = await client.PostAsync(messageEndpoint, CreateInitializeRequestContent(), timeoutCts.Token);
        Assert.AreEqual(HttpStatusCode.Accepted, initializeResponse.StatusCode);

        var messageEvent = await ReadNextSseEventAsync(reader, timeoutCts.Token);
        Assert.AreEqual("message", messageEvent.EventName);

        using var document = JsonDocument.Parse(messageEvent.Data);
        Assert.AreEqual("2.0", document.RootElement.GetProperty("jsonrpc").GetString());
        Assert.AreEqual("2024-11-05", document.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());
    }

    [TestMethod("Delete_TerminateSession: 新协议 DELETE 请求应终止会话")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task Delete_TerminateSession(HttpTransportType type)
    {
        // Arrange
        var package = await TestMcpFactory.Shared.CreateSimpleHttpAsync(type);

        // 建立连接
        await package.Client.ListToolsAsync();
        Assert.IsTrue(package.Client.IsConnected);

        // Act - 断开连接（内部会发送 DELETE 请求）
        await package.DisposeAsync();

        // Assert - Client 应已断开
        Assert.IsFalse(package.Client.IsConnected);
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    private static StringContent CreateInitializeRequestContent()
    {
        return new StringContent(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"legacy-test-client","version":"1.0.0"}}}
            """,
            Encoding.UTF8,
            "application/json");
    }

    private static Uri ResolveEndpoint(Uri baseEndpoint, string endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(baseEndpoint, endpoint);
    }

    private static async Task<SseEvent> ReadNextSseEventAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        string? eventName = null;
        string? data = null;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            Assert.IsNotNull(line, "SSE stream closed before receiving expected event.");

            if (line.Length == 0)
            {
                if (eventName is not null || data is not null)
                {
                    return new SseEvent(eventName ?? string.Empty, data ?? string.Empty);
                }

                continue;
            }

            if (line.StartsWith(':'))
            {
                continue;
            }

            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                eventName = line[7..];
                continue;
            }

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                data = line[6..];
            }
        }
    }

    private readonly record struct SseEvent(string EventName, string Data);
}
