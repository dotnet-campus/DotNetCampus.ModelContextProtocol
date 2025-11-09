using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Protocol;

[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(InitializeRequestParams))]
public partial class McpServerRequestJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(InitializeResult))]
public partial class McpServerResponseJsonContext : JsonSerializerContext;
