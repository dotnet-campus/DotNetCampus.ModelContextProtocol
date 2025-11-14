namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 列出工具请求参数<br/>
/// Sent from the client to request a list of tools the server has.
/// </summary>
public sealed record ListToolsRequestParams : PaginatedRequestParams;
