using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Hosting.Logging;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using static DotNetCampus.ModelContextProtocol.Protocol.RequestMethods;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// MCP 协议桥接器，处理来自客户端的所有 JSON-RPC 请求并路由到相应的处理器。
/// </summary>
internal sealed class McpProtocolBridge(McpServerContext context)
{
    public async ValueTask<JsonRpcResponse?> HandleRequestAsync(
        IServiceProvider services,
        JsonRpcRequest? request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return null;
        }

        // Log incoming request
        if (request.Id is null)
        {
            context.Logger.Debug($"[McpServer][Mcp] Received notification. Method={request.Method}");
        }
        else
        {
            context.Logger.Debug($"[McpServer][Mcp] Received request. Method={request.Method}, Id={request.Id}");
        }

        try
        {
            await context.Handlers.OnRequestReceivingAsync(request);
        }
        catch (Exception ex)
        {
            context.Logger.Error($"[McpServer][Mcp] An exception occurred in OnRequestReceivingAsync. Error={ex.Message}");
        }

        JsonRpcResponse? response;
        if (request.Id is null)
        {
            // Notification，不需要响应。
            await HandleNotificationCoreAsync(request);

            try
            {
                await context.Handlers.OnNotificationReceivedAsync(request);
            }
            catch (Exception ex)
            {
                context.Logger.Error($"[McpServer][Mcp] An exception occurred in OnNotificationReceivedAsync. Error={ex.Message}");
            }
            response = null;
        }
        else
        {
            // Request，需要响应。
            response = await HandleRequestCoreAsync(services, request, cancellationToken);

            try
            {
                await context.Handlers.OnResponseSentAsync(request, response);
            }
            catch (Exception ex)
            {
                context.Logger.Error($"[McpServer][Mcp] An exception occurred in OnResponseSentAsync. Error={ex.Message}");
            }

            // Log response
            if (response.Error is not null)
            {
                context.Logger.Debug($"[McpServer][Mcp] Sending error response. Method={request.Method}, Id={request.Id}, ErrorCode={response.Error.Code}");
            }
            else
            {
                context.Logger.Debug($"[McpServer][Mcp] Sending success response. Method={request.Method}, Id={request.Id}");
            }
        }

        return response;
    }

    /// <summary>
    /// 处理通知消息（id 为 null，不需要响应）。
    /// </summary>
    private async ValueTask HandleNotificationCoreAsync(JsonRpcRequest request)
    {
        switch (request.Method)
        {
            case NotificationsInitialized:
                context.Logger.Info($"[McpServer][Mcp] Client initialized notification received. Session is now fully established.");
                break;

            default:
                context.Logger.Warn($"[McpServer][Mcp] Received unsupported notification. Method={request.Method}");
                break;
        }

        await ValueTask.CompletedTask;
    }

    private async ValueTask<JsonRpcResponse> HandleRequestCoreAsync(
        IServiceProvider services,
        JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        return request.Method switch
        {
            null => new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = (int)JsonRpcErrorCode.InvalidRequest,
                    Message = "Json-RPC format error or missing method.",
                },
            },
            Initialize => await HandleRequestAsync(request, services, context.Handlers.HandleInitializeAsync,
                McpInternalJsonContext.Default.InitializeRequestParams, McpInternalJsonContext.Default.InitializeResult,
                cancellationToken),
            Ping => await HandleRequestAsync(request, services, context.Handlers.HandlePingAsync,
                McpInternalJsonContext.Default.PingRequestParams, McpInternalJsonContext.Default.EmptyObject,
                cancellationToken),
            LoggingSetLevel => await HandleRequestAsync(request, services, context.Handlers.HandleSetLoggingLevelAsync,
                McpInternalJsonContext.Default.SetLevelRequestParams, McpInternalJsonContext.Default.EmptyObject,
                cancellationToken),
            ToolsList => await HandleRequestAsync(request, services, context.Handlers.HandleListToolsAsync,
                McpInternalJsonContext.Default.ListToolsRequestParams, McpInternalJsonContext.Default.ListToolsResult,
                cancellationToken),
            ToolsCall => await HandleRequestAsync(request, services, context.Handlers.HandleCallToolAsync,
                McpInternalJsonContext.Default.CallToolRequestParams, McpInternalJsonContext.Default.CallToolResult,
                cancellationToken),
            ResourcesList => await HandleRequestAsync(request, services, context.Handlers.HandleListResourcesAsync,
                McpInternalJsonContext.Default.ListResourcesRequestParams, McpInternalJsonContext.Default.ListResourcesResult,
                cancellationToken),
            ResourcesTemplatesList => await HandleRequestAsync(request, services, context.Handlers.HandleListResourceTemplatesAsync,
                McpInternalJsonContext.Default.ListResourceTemplatesRequestParams, McpInternalJsonContext.Default.ListResourceTemplatesResult,
                cancellationToken),
            ResourcesRead => await HandleRequestAsync(request, services, context.Handlers.HandleReadResourceAsync,
                McpInternalJsonContext.Default.ReadResourceRequestParams, McpInternalJsonContext.Default.ReadResourceResult,
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
    }

    private async ValueTask<JsonRpcResponse> HandleRequestAsync<TParams, TResult>(
        JsonRpcRequest request,
        IServiceProvider services,
        Func<RequestContext<TParams>, CancellationToken, ValueTask<TResult>> handler,
        JsonTypeInfo<TParams> paramsTypeInfo, JsonTypeInfo<TResult> resultTypeInfo,
        CancellationToken cancellationToken)
    {
        var paramsElement = request.Params ?? EmptyObject.JsonElement;
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

}
