using dotnetCampus.Ipc.Context;
using dotnetCampus.Ipc.Pipes;
using DotNetCampus.ModelContextProtocol.Transports.Ipc;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 扩展 <see cref="McpServerBuilder"/> 以支持 DotNetCampus.Ipc 相关功能。
/// </summary>
public static class McpServerBuilderIpcExtensions
{
    /// <param name="builder">用于链式调用的 MCP 服务器生成器。</param>
    extension(McpServerBuilder builder)
    {
        /// <summary>
        /// 使用现有的 <see cref="IpcProvider"/> 搭建 MCP 传输层。
        /// </summary>
        /// <param name="ipcProvider">复用外部创建的 <see cref="IpcProvider"/>。</param>
        /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
        public McpServerBuilder WithDotNetCampusIpc(IpcProvider ipcProvider)
        {
            builder.WithTransport(m => new IpcServerTransport(m, ipcProvider));
            return builder;
        }

        /// <summary>
        /// 创建一个 IPC 服务器作为 MCP 的传输层。
        /// </summary>
        /// <param name="pipeName">本地服务名，将作为管道名，管道服务端名</param>
        /// <param name="ipcConfiguration"></param>
        /// <returns>用于链式调用的 MCP 服务器生成器。</returns>
        public McpServerBuilder WithDotNetCampusIpc(string pipeName, IpcConfiguration? ipcConfiguration = null)
        {
            builder.WithTransport(m => new IpcServerTransport(m, pipeName, ipcConfiguration));
            return builder;
        }
    }
}
