using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.Core;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;
using McpServerRequestJsonContext = DotNetCampus.ModelContextProtocol.CompilerServices.McpServerRequestJsonContext;
using McpServerResponseJsonContext = DotNetCampus.ModelContextProtocol.CompilerServices.McpServerResponseJsonContext;
using static DotNetCampus.ModelContextProtocol.Protocol.RequestMethods;

namespace DotNetCampus.ModelContextProtocol.Servers;

internal static class McpRequestDispatcher
{
    public static async Task<JsonRpcResponse> HandleRequestAsync(this McpRequestHandlers handlers,
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
            McpServerRequestJsonContext.Default.PingRequestParams, McpServerResponseJsonContext.Default.EmptyResult,
            cancellationToken),
        ToolsList => await request.HandleRequestAsync(services, handlers.ListTools,
            McpServerRequestJsonContext.Default.ListToolsRequestParams, McpServerResponseJsonContext.Default.ListToolsResult,
            cancellationToken),
        ToolsCall => await request.HandleRequestAsync(services, handlers.CallTool,
            McpServerRequestJsonContext.Default.CallToolRequestParams, McpServerResponseJsonContext.Default.CallToolResult,
            cancellationToken),
        LoggingSetLevel => await request.HandleRequestAsync(services, handlers.SetLoggingLevel,
            McpServerRequestJsonContext.Default.SetLevelRequestParams, McpServerResponseJsonContext.Default.EmptyResult,
            cancellationToken),
        _ => new JsonRpcResponse
        {
            Id = request.Id,
            Error = new JsonRpcError
            {
                Code = (int)JsonRpcErrorCode.MethodNotFound,
                Message = $"{request.Method} method is not supported.",
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
                null or EmptyResult => new JsonRpcResponse
                {
                    Id = request.Id,
                    // JSON-RPC 2.0 规范要求成功响应必须包含 result 字段，即使为空对象
                    Result = EmptyResult.JsonElement,
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
