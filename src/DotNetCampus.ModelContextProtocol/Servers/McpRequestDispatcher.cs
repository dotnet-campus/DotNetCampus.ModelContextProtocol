using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.Logging;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using DotNetCampus.ModelContextProtocol.Utils;
using static DotNetCampus.ModelContextProtocol.Protocol.RequestMethods;

namespace DotNetCampus.ModelContextProtocol.Servers;

internal static class McpRequestDispatcher
{
    public static async Task<JsonRpcResponse> HandleRequestAsync(this McpRequestHandlers handlers,
        ScopedServiceProvider services,
        JsonRpcRequest? request, CancellationToken cancellationToken = default)
    {
        if (request?.Id is null)
        {
            // Notification，不需要响应。
            await HandleNotificationInternalAsync(request);
            return JsonRpcResponse.NoResponse;
        }

        // Request，需要响应。
        return await HandleRequestInternalAsync(handlers, services, request, cancellationToken);
    }

    /// <summary>
    /// 处理通知消息（id 为 null，不需要响应）
    /// </summary>
    private static async ValueTask HandleNotificationInternalAsync(JsonRpcRequest? request)
    {
        if (request is null)
        {
            return;
        }

        switch (request.Method)
        {
            case NotificationsInitialized:
                // notifications/initialized - 客户端在初始化后发送的通知
                Log.Trace($"[McpServer][MCP] Received notifications {request.Method}.");
                break;

            default:
                Log.Warn($"[McpServer][MCP] Received notifications {request.Method}, but it is currently not supported.");
                break;
        }

        await ValueTask.CompletedTask;
    }

    private static async Task<JsonRpcResponse> HandleRequestInternalAsync(this McpRequestHandlers handlers,
        ScopedServiceProvider services,
        JsonRpcRequest? request, CancellationToken cancellationToken = default) => request?.Method switch
    {
        null => new JsonRpcResponse
        {
            Id = request?.Id,
            Error = new JsonRpcError
            {
                Code = (int)JsonRpcErrorCode.InvalidRequest,
                Message = "Json-RPC format error or missing method.",
            },
        },
        Initialize => await request.HandleRequestAsync(services, handlers.Initialize,
            McpServerRequestJsonContext.Default.InitializeRequestParams, McpServerResponseJsonContext.Default.InitializeResult,
            cancellationToken),
        Ping => await request.HandleRequestAsync(services, handlers.Ping,
            McpServerRequestJsonContext.Default.PingRequestParams, McpServerResponseJsonContext.Default.EmptyObject,
            cancellationToken),
        LoggingSetLevel => await request.HandleRequestAsync(services, handlers.SetLoggingLevel,
            McpServerRequestJsonContext.Default.SetLevelRequestParams, McpServerResponseJsonContext.Default.EmptyObject,
            cancellationToken),
        ToolsList => await request.HandleRequestAsync(services, handlers.ListTools,
            McpServerRequestJsonContext.Default.ListToolsRequestParams, McpServerResponseJsonContext.Default.ListToolsResult,
            cancellationToken),
        ToolsCall => await request.HandleRequestAsync(services, handlers.CallTool,
            McpServerRequestJsonContext.Default.CallToolRequestParams, McpServerResponseJsonContext.Default.CallToolResult,
            cancellationToken),
        ResourcesList => await request.HandleRequestAsync(services, handlers.ListResources,
            McpServerRequestJsonContext.Default.ListResourcesRequestParams, McpServerResponseJsonContext.Default.ListResourcesResult,
            cancellationToken),
        ResourcesTemplatesList => await request.HandleRequestAsync(services, handlers.ListResourceTemplates,
            McpServerRequestJsonContext.Default.ListResourceTemplatesRequestParams, McpServerResponseJsonContext.Default.ListResourceTemplatesResult,
            cancellationToken),
        ResourcesRead => await request.HandleRequestAsync(services, handlers.ReadResource,
            McpServerRequestJsonContext.Default.ReadResourceRequestParams, McpServerResponseJsonContext.Default.ReadResourceResult,
            cancellationToken),
        _ => new JsonRpcResponse
        {
            Id = request.Id,
            Error = new JsonRpcError
            {
                Code = (int)JsonRpcErrorCode.MethodNotFound,
                Message = $"{request.Method} method is currently not supported.",
            },
        },
    };

    private static async Task<JsonRpcResponse> HandleRequestAsync<TParams, TResult>(
        this JsonRpcRequest request,
        ScopedServiceProvider services,
        McpRequestHandler<TParams, TResult> handler,
        JsonTypeInfo<TParams> paramsTypeInfo, JsonTypeInfo<TResult> resultTypeInfo,
        CancellationToken cancellationToken)
    {
        if (!request.EnsureParams(out var paramsElement, out var errorResponse))
        {
            return errorResponse;
        }

        var requestParams = paramsElement.Deserialize(paramsTypeInfo);
        var requestContext = new RequestContext<TParams>(services, requestParams);

        try
        {
            var result = await handler(requestContext, cancellationToken);
            return result switch
            {
                null or EmptyObject => new JsonRpcResponse
                {
                    Id = request.Id,
                    // JSON-RPC 2.0 规范要求成功响应必须包含 result 字段，即使为空对象
                    Result = EmptyObject.JsonElement,
                },
                _ => new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = JsonSerializer.SerializeToElement(result, resultTypeInfo),
                },
            };
        }
        catch (ModelContextProtocolException ex)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = ex.JsonRpcErrorCode ?? (int)JsonRpcErrorCode.InternalError,
                    Message = ex.Message,
                    Data = McpExceptionData.From(ex).ToJsonElement(),
                },
            };
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = (int)JsonRpcErrorCode.InternalError,
                    Message = ex.Message,
                    Data = McpExceptionData.From(ex).ToJsonElement(),
                },
            };
        }
    }

    private static bool EnsureParams(this JsonRpcRequest request,
        out JsonElement paramsElement,
        [NotNullWhen(false)] out JsonRpcResponse? errorResponse)
    {
        if (request.Params is JsonElement element)
        {
            paramsElement = element;
            errorResponse = null;
            return true;
        }
        errorResponse = new JsonRpcResponse
        {
            Id = request.Id,
            Error = new JsonRpcError
            {
                Code = (int)JsonRpcErrorCode.InvalidParams,
                Message = "The params field is missing or not a valid JSON object.",
            },
        };
        paramsElement = default;
        return false;
    }
}
