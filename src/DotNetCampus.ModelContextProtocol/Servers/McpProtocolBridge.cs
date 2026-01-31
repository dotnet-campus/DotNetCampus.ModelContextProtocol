using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using static DotNetCampus.ModelContextProtocol.Protocol.RequestMethods;

namespace DotNetCampus.ModelContextProtocol.Servers;

internal class McpProtocolBridge(McpServerContext context)
{
    public async ValueTask<JsonRpcResponse?> HandleRequestAsync(
        IServiceProvider services,
        JsonRpcRequest? request, CancellationToken cancellationToken = default)
    {
        if (request?.Id is null)
        {
            // Notification，不需要响应。
            await HandleNotificationInternalAsync(request);
            return null;
        }

        // Request，需要响应。
        return await HandleRequestInternalAsync(services, request, cancellationToken);
    }

    /// <summary>
    /// 处理通知消息（id 为 null，不需要响应）
    /// </summary>
    private async ValueTask HandleNotificationInternalAsync(JsonRpcRequest? request)
    {
        if (request is null)
        {
            return;
        }

        switch (request.Method)
        {
            case NotificationsInitialized:
                // notifications/initialized - 客户端在初始化后发送的通知
                break;

            default:
                context.Logger.Warn($"[McpServer][Mcp] Received notifications {request.Method}, but it is currently not supported.");
                break;
        }

        await ValueTask.CompletedTask;
    }

    private async ValueTask<JsonRpcResponse> HandleRequestInternalAsync(
        IServiceProvider services,
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
        Initialize => await HandleRequestAsync(request, services, context.Handlers.Initialize,
            McpServerRequestJsonContext.Default.InitializeRequestParams, McpServerResponseJsonContext.Default.InitializeResult,
            cancellationToken),
        Ping => await HandleRequestAsync(request, services, context.Handlers.Ping,
            McpServerRequestJsonContext.Default.PingRequestParams, McpServerResponseJsonContext.Default.EmptyObject,
            cancellationToken),
        LoggingSetLevel => await HandleRequestAsync(request, services, context.Handlers.SetLoggingLevel,
            McpServerRequestJsonContext.Default.SetLevelRequestParams, McpServerResponseJsonContext.Default.EmptyObject,
            cancellationToken),
        ToolsList => await HandleRequestAsync(request, services, context.Handlers.ListTools,
            McpServerRequestJsonContext.Default.ListToolsRequestParams, McpServerResponseJsonContext.Default.ListToolsResult,
            cancellationToken),
        ToolsCall => await HandleRequestAsync(request, services, context.Handlers.CallTool,
            McpServerRequestJsonContext.Default.CallToolRequestParams, McpServerResponseJsonContext.Default.CallToolResult,
            cancellationToken),
        ResourcesList => await HandleRequestAsync(request, services, context.Handlers.ListResources,
            McpServerRequestJsonContext.Default.ListResourcesRequestParams, McpServerResponseJsonContext.Default.ListResourcesResult,
            cancellationToken),
        ResourcesTemplatesList => await HandleRequestAsync(request, services, context.Handlers.ListResourceTemplates,
            McpServerRequestJsonContext.Default.ListResourceTemplatesRequestParams, McpServerResponseJsonContext.Default.ListResourceTemplatesResult,
            cancellationToken),
        ResourcesRead => await HandleRequestAsync(request, services, context.Handlers.ReadResource,
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

    private async ValueTask<JsonRpcResponse> HandleRequestAsync<TParams, TResult>(
        JsonRpcRequest request,
        IServiceProvider services,
        McpRequestHandler<TParams, TResult> handler,
        JsonTypeInfo<TParams> paramsTypeInfo, JsonTypeInfo<TResult> resultTypeInfo,
        CancellationToken cancellationToken)
    {
        if (!EnsureParams(request, out var paramsElement, out var errorResponse))
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
        catch (McpServerException ex)
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

    private bool EnsureParams(JsonRpcRequest request,
        out JsonElement paramsElement,
        [NotNullWhen(false)] out JsonRpcResponse? errorResponse)
    {
        if (request.Params is { } element)
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
