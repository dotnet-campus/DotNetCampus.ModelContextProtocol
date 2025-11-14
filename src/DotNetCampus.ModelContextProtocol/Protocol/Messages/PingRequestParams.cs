namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// Ping 请求参数<br/>
/// A ping, issued by either the server or the client, to check that the other party is still alive.
/// The receiver must promptly respond, or else may be disconnected.
/// </summary>
public sealed record PingRequestParams : RequestParams
{
}
