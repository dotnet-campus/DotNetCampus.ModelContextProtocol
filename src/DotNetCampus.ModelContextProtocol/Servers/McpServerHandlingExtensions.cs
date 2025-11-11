using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DotNetCampus.ModelContextProtocol.Core;
using DotNetCampus.ModelContextProtocol.Messages;
using DotNetCampus.ModelContextProtocol.Protocol;
using McpServerRequestJsonContext = DotNetCampus.ModelContextProtocol.CompilerServices.McpServerRequestJsonContext;
using McpServerResponseJsonContext = DotNetCampus.ModelContextProtocol.CompilerServices.McpServerResponseJsonContext;

namespace DotNetCampus.ModelContextProtocol.Servers;

internal static class McpServerHandlingExtensions
{
    public static async Task<JsonRpcResponse> HandleRequestAsync(this McpServerHandlers handlers,
        JsonRpcRequest? request, CancellationToken cancellationToken = default) => request?.Method switch
    {
        null => new JsonRpcResponse
        {
            Error = new JsonRpcError
            {
                Code = 400,
                Message = "Json-RPC format error or missing method.",
            },
        },
        "initialize" => await request.HandleRequestAsync(handlers.InitializeHandler,
            McpServerRequestJsonContext.Default.InitializeRequestParams, McpServerResponseJsonContext.Default.InitializeResult,
            cancellationToken),
        "ping" => await request.HandleRequestAsync(handlers.PingHandler,
            McpServerRequestJsonContext.Default.PingRequestParams, McpServerResponseJsonContext.Default.NullResult,
            cancellationToken),
        _ => new JsonRpcResponse
        {
            Error = new JsonRpcError
            {
                Code = 400,
                Message = $"{request.Method} method is not supported.",
            },
        },
    };

    public static async ValueTask<JsonRpcResponse> WriteErrorResponseAsync(this Stream stream,
        int errorCode, string message, CancellationToken cancellationToken = default)
    {
        var errorResponse = new JsonRpcResponse
        {
            Error = new JsonRpcError
            {
                Code = errorCode,
                Message = message,
            },
        };

        await JsonSerializer.SerializeAsync(stream, errorResponse,
            McpServerResponseJsonContext.Default.JsonRpcResponse, cancellationToken);

        return errorResponse;
    }

    private static async Task<JsonRpcResponse> HandleRequestAsync<TParams, TResult>(
        this JsonRpcRequest request,
        McpRequestHandler<TParams, TResult> handler,
        JsonTypeInfo<TParams> paramsTypeInfo, JsonTypeInfo<TResult> resultTypeInfo,
        CancellationToken cancellationToken)
    {
        if (!request.EnsureParams(out var paramsElement, out var errorResponse))
        {
            return errorResponse;
        }

        var initializeRequestParams = paramsElement.Deserialize(paramsTypeInfo);
        var requestContext = new RequestContext<TParams>(initializeRequestParams);
        var result = await handler(requestContext, cancellationToken);

        return result switch
        {
            null or NullResult => new JsonRpcResponse
            {
                Id = request.Id,
            },
            _ => new JsonRpcResponse
            {
                Id = request.Id,
                Result = JsonSerializer.SerializeToElement(result, resultTypeInfo),
            },
        };
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
            Error = new JsonRpcError
            {
                Code = 400,
                Message = $"{request.Method} 方法缺少必要的参数。",
            },
        };
        paramsElement = default;
        return false;
    }
}
