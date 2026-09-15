using dotnetCampus.Ipc.Context;
using dotnetCampus.Ipc.Pipes;
using DotNetCampus.ModelContextProtocol.Transports.Ipc;

namespace DotNetCampus.ModelContextProtocol.Clients;

/// <summary>
/// 扩展 <see cref="McpClientBuilder"/> 以支持 DotNetCampus.Ipc 相关功能。
/// </summary>
public static class McpClientBuilderIpcExtensions
{
    /// <param name="builder">用于链式调用的 MCP 客户端生成器。</param>
    extension(McpClientBuilder builder)
    {
        /// <summary>
        /// 使用 DotNetCampus.Ipc 传输层连接到 MCP 服务器。
        /// </summary>
        /// <param name="serverPipeName">要连接的 MCP 服务器的管道名。</param>
        /// <param name="ipcConfiguration">IPC 配置。</param>
        /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
        public McpClientBuilder WithDotNetCampusIpc(string serverPipeName, IpcConfiguration? ipcConfiguration = null)
        {
            builder.WithTransport(m => new IpcClientTransport(m, serverPipeName, ipcConfiguration));
            return builder;
        }

        /// <summary>
        /// 使用现有的 <see cref="IpcProvider"/> 通过 DotNetCampus.Ipc 传输层连接到 MCP 服务器。
        /// </summary>
        /// <param name="ipcProvider">复用外部创建的 <see cref="IpcProvider"/>。</param>
        /// <param name="serverPipeName">要连接的 MCP 服务器的管道名。</param>
        /// <returns>用于链式调用的 MCP 客户端生成器。</returns>
        public McpClientBuilder WithDotNetCampusIpc(IpcProvider ipcProvider, string serverPipeName)
        {
            builder.WithTransport(m => new IpcClientTransport(m, ipcProvider, serverPipeName));
            return builder;
        }
    }
}
