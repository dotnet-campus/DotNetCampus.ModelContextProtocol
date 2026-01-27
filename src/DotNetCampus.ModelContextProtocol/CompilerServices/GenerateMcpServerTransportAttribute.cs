using System.Diagnostics;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 在一个空的分部类上标记，源生成器将会为该类型生成完整的 MCP 服务端传输层实现。<br/>
/// 所有将生成的类型都会依赖不同的框架或库，请确保生成前已安装对应的框架或库，否则生成的代码将无法编译通过。
/// </summary>
/// <param name="packageId">指定要生成的传输层来自于哪个 NuGet 包里的某个协议。</param>
[Conditional("FOR_SOURCE_GENERATION_ONLY")]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateMcpServerTransportAttribute(McpServerTransportPackageId packageId) : Attribute
{
    /// <summary>
    /// 要生成的传输层来自于哪个 NuGet 包里的某个协议。
    /// </summary>
    public McpServerTransportPackageId PackageId { get; } = packageId;
}

/// <summary>
/// 在一个空的分部类上标记，源生成器将会为该类型生成完整的 MCP 客户端传输层实现。<br/>
/// 所有将生成的类型都会依赖不同的框架或库，请确保生成前已安装对应的框架或库，否则生成的代码将无法编译通过。
/// </summary>
/// <param name="packageId">指定要生成的传输层来自于哪个 NuGet 包里的某个协议。</param>
[Conditional("FOR_SOURCE_GENERATION_ONLY")]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateMcpClientTransportAttribute(McpClientTransportPackageId packageId) : Attribute
{
    /// <summary>
    /// 要生成的传输层来自于哪个 NuGet 包里的某个协议。
    /// </summary>
    public McpClientTransportPackageId PackageId { get; } = packageId;
}

/// <summary>
/// 目前已支持通过源生成器生成的 MCP 服务端传输层的包 Id。
/// </summary>
public enum McpServerTransportPackageId
{
    /// <summary>
    /// 使用 TouchSocket 的 Http 传输层。
    /// </summary>
    TouchSocketHttp,

    /// <summary>
    /// 使用 DotNetCampus.Ipc 的 IPC 传输层。
    /// </summary>
    DotNetCampusIpc,
}

/// <summary>
/// 目前已支持通过源生成器生成的 MCP 客户端传输层的包 Id。
/// </summary>
public enum McpClientTransportPackageId
{
    /// <summary>
    /// 使用 DotNetCampus.Ipc 的 IPC 传输层。
    /// </summary>
    DotNetCampusIpc,
}
