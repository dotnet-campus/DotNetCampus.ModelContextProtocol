using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// MCP 服务器的传输层。
/// </summary>
public interface IServerTransport : IAsyncDisposable
{
    /// <summary>
    /// 启动传输层（开始监听来自客户端的连接）。
    /// </summary>
    /// <param name="startingCancellationToken">
    /// 启动取消令牌。当此令牌取消时，应终止 MCP 服务器的启动；而一旦启动完成，此令牌将不再起作用。
    /// </param>
    /// <param name="runningCancellationToken">
    /// 运行取消令牌。当此令牌取消时，应终止 MCP 服务器。
    /// </param>
    /// <returns>
    /// 嵌套异步任务。<br/>
    /// 第一层任务完成时，表示传输层已成功启动并开始监听连接。<br/>
    /// 第二层任务完成时，表示传输层已停止运行（即调用了 <see cref="McpServer.StopAsync"/> 方法并完成）。<br/>
    /// </returns>
    Task<Task> StartAsync(CancellationToken startingCancellationToken, CancellationToken runningCancellationToken);
}
