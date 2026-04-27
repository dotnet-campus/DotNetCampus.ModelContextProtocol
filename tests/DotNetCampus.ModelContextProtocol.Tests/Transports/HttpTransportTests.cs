using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Transports.Http;
using DotNetCampus.ModelContextProtocol.Transports.TouchSocket;

namespace DotNetCampus.ModelContextProtocol.Tests.Transports;

/// <summary>
/// HTTP 传输层特性测试：SSE 和 POST 通信模式的特定测试。
/// </summary>
[TestClass]
public class HttpTransportTests
{
    [TestMethod("DefaultOptions_EnableLegacySseCompatibility: 默认开启 2024-11-05 服务端兼容")]
    public void DefaultOptions_EnableLegacySseCompatibility()
    {
        var localHostOptions = new LocalHostHttpServerTransportOptions
        {
            Port = 9527,
        };
        var touchSocketOptions = new TouchSocketHttpServerTransportOptions
        {
            Listen = ["127.0.0.1:9527"],
        };
        var externalTouchSocketOptions = new ExternalTouchSocketHttpServerTransportOptions();

        Assert.IsTrue(localHostOptions.IsCompatibleWithSse);
        Assert.AreEqual("/mcp/sse", localHostOptions.SseEndPoint);
        Assert.AreEqual("/mcp/messages", localHostOptions.SseMessageEndPoint);

        Assert.IsTrue(touchSocketOptions.IsCompatibleWithSse);
        Assert.AreEqual("/mcp/sse", touchSocketOptions.SseEndPoint);
        Assert.AreEqual("/mcp/messages", touchSocketOptions.SseMessageEndPoint);

        Assert.IsTrue(externalTouchSocketOptions.IsCompatibleWithSse);
        Assert.AreEqual("/mcp/sse", externalTouchSocketOptions.SseEndPoint);
        Assert.AreEqual("/mcp/messages", externalTouchSocketOptions.SseMessageEndPoint);
    }

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

    [TestMethod("StreamableHttp_NegotiatedVersionMustMatchSubsequentHeader: 协商结果应贯穿后续请求")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task StreamableHttp_NegotiatedVersionMustMatchSubsequentHeader(HttpTransportType type)
    {
        await using var package = await TestMcpFactory.Shared.CreateSimpleHttpAsync(type);
        using var client = CreateHttpClient();

        using var initializeRequest = CreateStreamableHttpRequest(HttpMethod.Post, package.Endpoint);
        initializeRequest.Content = CreateInitializeRequestContent("2025-06-18");

        using var initializeResponse = await client.SendAsync(initializeRequest);

        Assert.AreEqual(HttpStatusCode.OK, initializeResponse.StatusCode);
        Assert.IsTrue(initializeResponse.Headers.TryGetValues("Mcp-Session-Id", out var sessionHeaders));
        var sessionId = sessionHeaders.Single();

        using (var document = JsonDocument.Parse(await initializeResponse.Content.ReadAsStringAsync()))
        {
            Assert.AreEqual("2025-06-18", document.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());
        }

        using var mismatchRequest = CreateStreamableHttpRequest(HttpMethod.Post, package.Endpoint);
        mismatchRequest.Headers.Add("Mcp-Session-Id", sessionId);
        mismatchRequest.Headers.Add("Mcp-Protocol-Version", "2025-11-25");
        mismatchRequest.Content = CreateInitializedNotificationContent();

        using var mismatchResponse = await client.SendAsync(mismatchRequest);

        Assert.AreEqual(HttpStatusCode.BadRequest, mismatchResponse.StatusCode);
    }

    [TestMethod("StreamableHttp_GetAndDeleteMustMatchNegotiatedHeader: GET 与 DELETE 也应遵守协商结果")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task StreamableHttp_GetAndDeleteMustMatchNegotiatedHeader(HttpTransportType type)
    {
        await using var package = await TestMcpFactory.Shared.CreateSimpleHttpAsync(type);
        using var client = CreateHttpClient();

        using var initializeRequest = CreateStreamableHttpRequest(HttpMethod.Post, package.Endpoint);
        initializeRequest.Content = CreateInitializeRequestContent("2025-06-18");

        using var initializeResponse = await client.SendAsync(initializeRequest);

        Assert.AreEqual(HttpStatusCode.OK, initializeResponse.StatusCode);
        Assert.IsTrue(initializeResponse.Headers.TryGetValues("Mcp-Session-Id", out var sessionHeaders));
        var sessionId = sessionHeaders.Single();

        using var getRequest = CreateStreamableHttpRequest(HttpMethod.Get, package.Endpoint);
        getRequest.Headers.Add("Mcp-Session-Id", sessionId);
        getRequest.Headers.Add("Mcp-Protocol-Version", "2025-11-25");

        using var getResponse = await client.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);

        Assert.AreEqual(HttpStatusCode.BadRequest, getResponse.StatusCode);

        using var deleteRequest = CreateStreamableHttpRequest(HttpMethod.Delete, package.Endpoint);
        deleteRequest.Headers.Add("Mcp-Session-Id", sessionId);
        deleteRequest.Headers.Add("Mcp-Protocol-Version", "2025-11-25");

        using var deleteResponse = await client.SendAsync(deleteRequest);

        Assert.AreEqual(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }

    [TestMethod("StreamableHttp_BatchRequest_IsExplicitlyRejected: batch 请求边界应明确")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task StreamableHttp_BatchRequest_IsExplicitlyRejected(HttpTransportType type)
    {
        await using var package = await TestMcpFactory.Shared.CreateSimpleHttpAsync(type);
        using var client = CreateHttpClient();

        using var request = CreateStreamableHttpRequest(HttpMethod.Post, package.Endpoint);
        request.Content = new StringContent(
            """
            [{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"batch-client","version":"1.0.0"}}}]
            """,
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod("StreamableHttp_UnknownFutureVersionNegotiatesDownToCurrent: 未知未来版本应回落到当前支持版本")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    [DataRow(HttpTransportType.TouchSocket, DisplayName = "TouchSocket")]
    public async Task StreamableHttp_UnknownFutureVersionNegotiatesDownToCurrent(HttpTransportType type)
    {
        await using var package = await TestMcpFactory.Shared.CreateSimpleHttpAsync(type);
        using var client = CreateHttpClient();

        using var initializeRequest = CreateStreamableHttpRequest(HttpMethod.Post, package.Endpoint);
        initializeRequest.Content = CreateInitializeRequestContent("2026-01-01");

        using var initializeResponse = await client.SendAsync(initializeRequest);

        Assert.AreEqual(HttpStatusCode.OK, initializeResponse.StatusCode);

        using var document = JsonDocument.Parse(await initializeResponse.Content.ReadAsStringAsync());
        Assert.AreEqual("2025-11-25", document.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());
    }

    [TestMethod("StreamableHttp_BlankProtocolVersionHeaderIsRejected: 空白协议版本头应视为无效")]
    [DataRow(HttpTransportType.LocalHost, DisplayName = "LocalHost")]
    public async Task StreamableHttp_BlankProtocolVersionHeaderIsRejected(HttpTransportType type)
    {
        await using var package = await TestMcpFactory.Shared.CreateSimpleHttpAsync(type);
        using var client = CreateHttpClient();

        using var initializeRequest = CreateStreamableHttpRequest(HttpMethod.Post, package.Endpoint);
        initializeRequest.Content = CreateInitializeRequestContent("2025-06-18");

        using var initializeResponse = await client.SendAsync(initializeRequest);

        Assert.AreEqual(HttpStatusCode.OK, initializeResponse.StatusCode);
        Assert.IsTrue(initializeResponse.Headers.TryGetValues("Mcp-Session-Id", out var sessionHeaders));
        var sessionId = sessionHeaders.Single();

        var statusLine = await SendRawHttpRequestAsync(package.Endpoint, BuildBlankProtocolVersionRequest(package.Endpoint, sessionId));

        StringAssert.Contains(statusLine, "400");
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    private static StringContent CreateInitializeRequestContent(string protocolVersion = "2024-11-05")
    {
        return new StringContent(
            $"{{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{{\"protocolVersion\":\"{protocolVersion}\",\"capabilities\":{{}},\"clientInfo\":{{\"name\":\"legacy-test-client\",\"version\":\"1.0.0\"}}}}}}",
            Encoding.UTF8,
            "application/json");
    }

    private static StringContent CreateInitializedNotificationContent()
    {
        return new StringContent(
            """
            {"jsonrpc":"2.0","method":"notifications/initialized"}
            """,
            Encoding.UTF8,
            "application/json");
    }

    private static HttpRequestMessage CreateStreamableHttpRequest(HttpMethod method, Uri endpoint)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return request;
    }

    private static Uri ResolveEndpoint(Uri baseEndpoint, string endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(baseEndpoint, endpoint);
    }

    private static async Task<string> SendRawHttpRequestAsync(Uri endpoint, string rawRequest)
    {
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(endpoint.Host, endpoint.Port);
        await using var stream = tcpClient.GetStream();

        var requestBytes = Encoding.ASCII.GetBytes(rawRequest);
        await stream.WriteAsync(requestBytes);
        await stream.FlushAsync();

        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        return await reader.ReadLineAsync() ?? string.Empty;
    }

    private static string BuildBlankProtocolVersionRequest(Uri endpoint, string sessionId)
    {
        const string body = "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}";
        var hostHeader = endpoint.IsDefaultPort ? endpoint.Host : $"{endpoint.Host}:{endpoint.Port}";
        var contentLength = Encoding.UTF8.GetByteCount(body);

        return string.Join("\r\n",
        [
            $"POST {endpoint.PathAndQuery} HTTP/1.1",
            $"Host: {hostHeader}",
            "Accept: application/json",
            "Accept: text/event-stream",
            $"Mcp-Session-Id: {sessionId}",
            "Mcp-Protocol-Version: ",
            "Content-Type: application/json",
            $"Content-Length: {contentLength}",
            "Connection: close",
            string.Empty,
            body,
        ]);
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
