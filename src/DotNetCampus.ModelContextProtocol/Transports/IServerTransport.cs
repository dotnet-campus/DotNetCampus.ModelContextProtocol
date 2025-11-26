namespace DotNetCampus.ModelContextProtocol.Transports;

/// <summary>
/// MCP 服务器的传输层。
/// </summary>
public interface IServerTransport : IAsyncDisposable
{
    /// <summary>
    /// 启动传输层（开始监听来自客户端的连接）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>
    /// 嵌套异步任务。<br/>
    /// 第一层任务完成时，表示传输层已成功启动并开始监听连接。<br/>
    /// 第二层任务完成时，表示传输层已停止运行（即调用了 <see cref="StopAsync"/> 方法并完成）。<br/>
    /// </returns>
    Task<Task> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止传输层（停止监听来自客户端的连接，断开所有现有连接，并回收所有资源）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示停止操作已完成的任务。</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}
