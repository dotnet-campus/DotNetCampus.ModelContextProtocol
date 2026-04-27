using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Protocol;
using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Http.Legacy;

/// <summary>
/// 统一改写 2024-11-05 旧传输层下的 initialize 响应。
/// </summary>
public static class LegacySseInitializeResponseAdapter
{
    internal static ProtocolVersion ProtocolVersion { get; } = ProtocolVersion.Minimum;

    /// <summary>
    /// 将 initialize 响应改写为 2024-11-05 旧传输层期望的协议版本。
    /// </summary>
    public static JsonRpcResponse Adapt(JsonRpcResponse response)
    {
        if (response.Result is not { ValueKind: JsonValueKind.Object } resultElement)
        {
            return response;
        }

        var initializeResult = resultElement.Deserialize(McpInternalJsonContext.Default.InitializeResult);
        if (initializeResult is null)
        {
            return response;
        }

        var adapted = initializeResult with { ProtocolVersion = ProtocolVersion };
        return response with
        {
            Result = JsonSerializer.SerializeToElement(adapted, McpInternalJsonContext.Default.InitializeResult),
        };
    }
}
