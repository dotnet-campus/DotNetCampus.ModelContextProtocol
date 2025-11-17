using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// MCP 服务器基本元素（提示词、资源、工具）的创建模式。
/// </summary>
public enum CreationMode
{
    /// <summary>
    /// 只调用创建委托一次，并在整个 <see cref="McpServer"/> 生命周期内重用该实例。
    /// </summary>
    Singleton,

    /// <summary>
    /// 每次调用时，都会调用创建委托，根据委托内的实现决定是复用还是新建实例。
    /// </summary>
    Transient,
}
