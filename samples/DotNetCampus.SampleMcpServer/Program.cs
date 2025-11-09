using System.Text.Json;
using DotNetCampus.Logging;
using DotNetCampus.Logging.Attributes;
using DotNetCampus.Logging.Writers;
using DotNetCampus.ModelContextProtocol.Messages;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.SampleMcpServer;

internal class Program
{
    private static async Task Main(string[] args)
    {
        new LoggerBuilder()
            .WithMemoryCache()
            .WithLevel(LogLevel.Trace)
            .AddConsoleLogger(b => b
                .WithThreadSafe(LogWritingThreadMode.ProducerConsumer)
                .FilterConsoleTagsFromCommandLineArgs(args))
            .AddBridge(LoggerBridgeLinker.Default)
            .Build()
            .IntoGlobalStaticLog();

        Console.WriteLine("Starting Sample MCP Server...");

        var httpTransport = McpServer.CreateHttpServerTransport("http://localhost:5943/");

        await httpTransport.StartAsync(async requestDoc =>
        {
            try
            {
                var root = requestDoc.RootElement;

                // 解析通用字段
                var method = root.GetProperty("method").GetString();
                var id = root.TryGetProperty("id", out var idProp) ? idProp.Clone() : (JsonElement?)null;

                Console.WriteLine($"Processing request: {method}");

                // 处理 initialize 请求
                if (method == "initialize")
                {
                    var response = new JsonRpcResponse
                    {
                        Id = id?.ValueKind == JsonValueKind.String ? id.Value.GetString() :
                            id?.ValueKind == JsonValueKind.Number ? (object)id.Value.GetInt32() : null,
                        Result = new InitializeResult
                        {
                            ProtocolVersion = "2025-06-18",
                            Capabilities = new ServerCapabilities
                            {
                                Tools = new ToolsCapability { ListChanged = true },
                                Resources = new ResourcesCapability { ListChanged = true, Subscribe = true },
                                Prompts = new PromptsCapability { ListChanged = true }
                            },
                            ServerInfo = new ServerInfo { Name = "DotNetCampus.SampleMcpServer", Version = "0.1.0" },
                            Instructions = "这是一个示例 MCP 服务器，用于测试 MCP Inspector"
                        }
                    };

                    return JsonDocument.Parse(JsonSerializer.Serialize(response));
                }

                // 处理 ping 请求
                if (method == "ping")
                {
                    var response = new JsonRpcResponse
                    {
                        Id = id?.ValueKind == JsonValueKind.String ? id.Value.GetString() :
                            id?.ValueKind == JsonValueKind.Number ? (object)id.Value.GetInt32() : null,
                        Result = new { }
                    };

                    return JsonDocument.Parse(JsonSerializer.Serialize(response));
                }

                // 处理 tools/list 请求
                if (method == "tools/list")
                {
                    var response = new JsonRpcResponse
                    {
                        Id = id?.ValueKind == JsonValueKind.String ? id.Value.GetString() :
                            id?.ValueKind == JsonValueKind.Number ? (object)id.Value.GetInt32() : null,
                        Result = new
                        {
                            tools = new[]
                            {
                                new
                                {
                                    name = "echo",
                                    description = "回显输入的文本",
                                    inputSchema = new
                                    {
                                        type = "object",
                                        properties = new { message = new { type = "string", description = "要回显的消息" } },
                                        required = new[] { "message" }
                                    }
                                }
                            }
                        }
                    };

                    return JsonDocument.Parse(JsonSerializer.Serialize(response));
                }

                // 处理 resources/list 请求
                if (method == "resources/list")
                {
                    var response = new JsonRpcResponse
                    {
                        Id = id?.ValueKind == JsonValueKind.String ? id.Value.GetString() :
                            id?.ValueKind == JsonValueKind.Number ? (object)id.Value.GetInt32() : null,
                        Result = new { resources = Array.Empty<object>() }
                    };

                    return JsonDocument.Parse(JsonSerializer.Serialize(response));
                }

                // 处理 prompts/list 请求
                if (method == "prompts/list")
                {
                    var response = new JsonRpcResponse
                    {
                        Id = id?.ValueKind == JsonValueKind.String ? id.Value.GetString() :
                            id?.ValueKind == JsonValueKind.Number ? (object)id.Value.GetInt32() : null,
                        Result = new { prompts = Array.Empty<object>() }
                    };

                    return JsonDocument.Parse(JsonSerializer.Serialize(response));
                }

                // 未知方法
                var errorResponse = new JsonRpcResponse
                {
                    Id = id?.ValueKind == JsonValueKind.String ? id.Value.GetString() :
                        id?.ValueKind == JsonValueKind.Number ? (object)id.Value.GetInt32() : null,
                    Error = new JsonRpcError { Code = -32601, Message = $"Method not found: {method}" }
                };

                return JsonDocument.Parse(JsonSerializer.Serialize(errorResponse));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request: {ex}");

                var errorResponse = new JsonRpcResponse { Error = new JsonRpcError { Code = -32603, Message = $"Internal error: {ex.Message}" } };

                return JsonDocument.Parse(JsonSerializer.Serialize(errorResponse));
            }
        });
    }
}

[ImportLoggerBridge<DotNetCampus.ModelContextProtocol.Logging.ILoggerBridge>]
internal partial class LoggerBridgeLinker;
