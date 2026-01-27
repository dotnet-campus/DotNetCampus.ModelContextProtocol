using System.Diagnostics;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// 在一个空的分部类上标记，源生成器将会为该类型生成完整的 MCP 传输层实现。<br/>
/// 所有将生成的类型都会依赖不同的框架或库，请确保生成前已安装对应的框架或库，否则生成的代码将无法编译通过。
/// </summary>
/// <param name="packageId">指定要生成的传输层来自于哪个 NuGet 包里的某个协议。</param>
/// <param name="side">制定要生成服务端还是客户端的传输层。</param>
[Conditional("FOR_SOURCE_GENERATION_ONLY")]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateMcpTransportAttribute(McpTransportPackageId packageId, McpSide side) : Attribute
{
    /// <summary>
    /// 要生成的传输层来自于哪个 NuGet 包里的某个协议。
    /// </summary>
    public McpTransportPackageId PackageId { get; } = packageId;

    /// <summary>
    /// 要生成服务端还是客户端的传输层。
    /// </summary>
    public McpSide Side { get; } = side;
}

/// <summary>
/// 目前已支持通过源生成器生成的 MCP 传输层的包 Id。
/// </summary>
public enum McpTransportPackageId
{
    /// <summary>
    /// 使用 TouchSocket 的 Http 传输层。
    /// </summary>
    TouchSocketHttp,
}
