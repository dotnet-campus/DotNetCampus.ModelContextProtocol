using DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 表示 MCP 服务器资源的接口。
/// </summary>
public interface IMcpServerResource
{
    /// <summary>
    /// 获取资源在 MCP 协议中的名称。
    /// </summary>
    public string ResourceName { get; }

    /// <summary>
    /// 读取 MCP 服务器资源的方法。
    /// </summary>
    /// <param name="context">读取资源时的上下文信息。</param>
    /// <returns>表示资源读取结果的对象。</returns>
    ValueTask<ReadResourceResult> ReadResource(IMcpServerReadResourceContext context);
}
