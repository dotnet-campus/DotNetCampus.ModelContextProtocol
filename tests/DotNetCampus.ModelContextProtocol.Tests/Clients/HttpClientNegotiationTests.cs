using System.Net;
using System.Text;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.Clients;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Transports.Http;

namespace DotNetCampus.ModelContextProtocol.Tests.Clients;

[TestClass]
public class HttpClientNegotiationTests
{
    [TestMethod("HttpClient_FirstGetCarriesNegotiatedProtocolVersion: 初始化后的首个 GET 必须携带协商版本头")]
    public async Task HttpClient_FirstGetCarriesNegotiatedProtocolVersion()
    {
        var handler = new RecordingMcpServerHandler();
        using var httpClient = new HttpClient(handler);
        await using var client = new McpClientBuilder()
            .WithClientInfo("test-client", "1.0.0")
            .WithHttp(new HttpClientTransportOptions
            {
                ServerUrl = "http://localhost/mcp",
                HttpClient = httpClient,
            })
            .Build();

        _ = await client.ListToolsAsync();

        await handler.WaitForFirstGetAsync();

        Assert.IsNotNull(handler.FirstGetProtocolVersion);
        Assert.AreEqual("2025-06-18", handler.FirstGetProtocolVersion);
    }

    [TestMethod("HttpClient_ReinitializesWhenSessionRequestReturns404: 带会话 ID 的请求收到 404 后应自动重建会话")]
    public async Task HttpClient_ReinitializesWhenSessionRequestReturns404()
    {
        var handler = new SessionRecoveryHandler();
        using var httpClient = new HttpClient(handler);
        await using var client = new McpClientBuilder()
            .WithClientInfo("test-client", "1.0.0")
            .WithHttp(new HttpClientTransportOptions
            {
                ServerUrl = "http://localhost/mcp",
                HttpClient = httpClient,
            })
            .Build();

        var result = await client.ListToolsAsync();

        Assert.AreEqual(0, result.Tools.Count);
        Assert.AreEqual(2, handler.InitializeRequestCount);
        Assert.IsFalse(handler.SecondInitializeHadSessionIdHeader);
    }

    [TestMethod("HttpClient_RejectsUnknownFutureNegotiatedVersion: 客户端不应接受未知未来版本")]
    public async Task HttpClient_RejectsUnknownFutureNegotiatedVersion()
    {
        var handler = new UnsupportedVersionHandler();
        using var httpClient = new HttpClient(handler);
        await using var client = new McpClientBuilder()
            .WithClientInfo("test-client", "1.0.0")
            .WithHttp(new HttpClientTransportOptions
            {
                ServerUrl = "http://localhost/mcp",
                HttpClient = httpClient,
            })
            .Build();

        var exception = await Assert.ThrowsExceptionAsync<McpClientException>(() => client.ListToolsAsync());

        StringAssert.Contains(exception.Message, "不支持的协议版本");
        Assert.IsTrue(handler.DeleteRequested);
        Assert.IsTrue(handler.DeleteHadSessionIdHeader);
    }

    private sealed class RecordingMcpServerHandler : HttpMessageHandler
    {
        private const string SessionId = "session-1";
        private readonly TaskCompletionSource _firstGetReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? FirstGetProtocolVersion { get; private set; }

        public async Task WaitForFirstGetAsync()
        {
            var completedTask = await Task.WhenAny(_firstGetReceived.Task, Task.Delay(TimeSpan.FromSeconds(2)));
            if (completedTask != _firstGetReceived.Task)
            {
                Assert.Fail("未观察到初始化后的 GET 请求。");
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                if (FirstGetProtocolVersion is null)
                {
                    FirstGetProtocolVersion = GetSingleHeaderValue(request, "Mcp-Protocol-Version");
                    _firstGetReceived.TrySetResult();
                }

                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)
                {
                    RequestMessage = request,
                };
            }

            if (request.Method == HttpMethod.Delete)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                };
            }

            var requestContent = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (requestContent.Contains("\"method\":\"initialize\"", StringComparison.Ordinal))
            {
                var requestId = GetRequestIdLiteral(requestContent);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StreamContent(new DelayedReadMemoryStream(
                        Encoding.UTF8.GetBytes(
                            $"{{\"jsonrpc\":\"2.0\",\"id\":{requestId},\"result\":{{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{{\"logging\":{{}}}},\"serverInfo\":{{\"name\":\"TestServer\",\"version\":\"1.0.0\"}}}}}}"),
                        300)),
                };
                response.Content.Headers.ContentType = new("application/json");
                response.Headers.TryAddWithoutValidation("Mcp-Session-Id", SessionId);
                return response;
            }

            if (requestContent.Contains("\"method\":\"notifications/initialized\"", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    RequestMessage = request,
                };
            }

            if (requestContent.Contains("\"method\":\"tools/list\"", StringComparison.Ordinal))
            {
                var requestId = GetRequestIdLiteral(requestContent);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StringContent(
                        $"{{\"jsonrpc\":\"2.0\",\"id\":{requestId},\"result\":{{\"tools\":[]}}}}",
                        Encoding.UTF8,
                        "application/json"),
                };
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                RequestMessage = request,
            };
        }

        private static string? GetSingleHeaderValue(HttpRequestMessage request, string headerName)
        {
            if (request.Headers.TryGetValues(headerName, out var values))
            {
                return values.SingleOrDefault();
            }

            return null;
        }
    }

    private sealed class SessionRecoveryHandler : HttpMessageHandler
    {
        private int _initializeRequestCount;

        public int InitializeRequestCount => _initializeRequestCount;

        public bool SecondInitializeHadSessionIdHeader { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)
                {
                    RequestMessage = request,
                };
            }

            if (request.Method == HttpMethod.Delete)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                };
            }

            var requestContent = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (requestContent.Contains("\"method\":\"initialize\"", StringComparison.Ordinal))
            {
                _initializeRequestCount++;
                if (_initializeRequestCount == 2)
                {
                    SecondInitializeHadSessionIdHeader = request.Headers.Contains("Mcp-Session-Id");
                }

                var requestId = GetRequestIdLiteral(requestContent);

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StringContent(
                        $"{{\"jsonrpc\":\"2.0\",\"id\":{requestId},\"result\":{{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{{\"logging\":{{}}}},\"serverInfo\":{{\"name\":\"TestServer\",\"version\":\"1.0.0\"}}}}}}",
                        Encoding.UTF8,
                        "application/json"),
                };
                response.Headers.TryAddWithoutValidation("Mcp-Session-Id", _initializeRequestCount == 1 ? "session-1" : "session-2");
                return response;
            }

            if (requestContent.Contains("\"method\":\"notifications/initialized\"", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    RequestMessage = request,
                };
            }

            if (requestContent.Contains("\"method\":\"tools/list\"", StringComparison.Ordinal))
            {
                var sessionId = GetSingleHeaderValue(request, "Mcp-Session-Id");
                if (sessionId == "session-1")
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        RequestMessage = request,
                    };
                }

                var requestId = GetRequestIdLiteral(requestContent);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StringContent(
                        $"{{\"jsonrpc\":\"2.0\",\"id\":{requestId},\"result\":{{\"tools\":[]}}}}",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                RequestMessage = request,
            };
        }
    }

    private sealed class UnsupportedVersionHandler : HttpMessageHandler
    {
        public bool DeleteRequested { get; private set; }

        public bool DeleteHadSessionIdHeader { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Delete)
            {
                DeleteRequested = true;
                DeleteHadSessionIdHeader = request.Headers.Contains("Mcp-Session-Id");
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                };
            }

            var requestContent = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (requestContent.Contains("\"method\":\"initialize\"", StringComparison.Ordinal))
            {
                var requestId = GetRequestIdLiteral(requestContent);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StringContent(
                        $"{{\"jsonrpc\":\"2.0\",\"id\":{requestId},\"result\":{{\"protocolVersion\":\"2026-01-01\",\"capabilities\":{{\"logging\":{{}}}},\"serverInfo\":{{\"name\":\"TestServer\",\"version\":\"1.0.0\"}}}}}}",
                        Encoding.UTF8,
                        "application/json"),
                };
                response.Headers.TryAddWithoutValidation("Mcp-Session-Id", "session-1");
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                RequestMessage = request,
            };
        }
    }

    private static string? GetSingleHeaderValue(HttpRequestMessage request, string headerName)
    {
        if (request.Headers.TryGetValues(headerName, out var values))
        {
            return values.SingleOrDefault();
        }

        return null;
    }

    private static string GetRequestIdLiteral(string requestContent)
    {
        using var document = JsonDocument.Parse(requestContent);
        return document.RootElement.GetProperty("id").GetRawText();
    }

    private sealed class DelayedReadMemoryStream : MemoryStream
    {
        private readonly int _delayMilliseconds;
        private bool _delayApplied;

        public DelayedReadMemoryStream(byte[] buffer, int delayMilliseconds)
            : base(buffer)
        {
            _delayMilliseconds = delayMilliseconds;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await DelayOnceAsync(cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await DelayOnceAsync(cancellationToken);
            return await base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        private Task DelayOnceAsync(CancellationToken cancellationToken)
        {
            if (_delayApplied)
            {
                return Task.CompletedTask;
            }

            _delayApplied = true;
            return Task.Delay(_delayMilliseconds, cancellationToken);
        }
    }
}