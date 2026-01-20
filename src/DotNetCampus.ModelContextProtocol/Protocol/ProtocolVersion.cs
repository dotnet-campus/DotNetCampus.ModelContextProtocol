namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// 协议版本信息<br/>
/// Protocol version information
/// </summary>
internal static class ProtocolVersion
{
    /// <summary>
    /// 当前使用的协议版本<br/>
    /// The currently used protocol version
    /// </summary>
    internal const string Current = "2025-11-25";

    /// <summary>
    /// 最早能兼容的协议版本<br/>
    /// The earliest compatible protocol version
    /// </summary>
    internal const string EarliestCompatible = "2024-11-05";

    /// <summary>
    /// 支持的协议版本列表<br/>
    /// List of supported protocol versions
    /// </summary>
    internal static IReadOnlyList<string> SupportedVersions { get; } =
    [
        "2025-11-25",
        "2025-06-18",
        "2025-03-26",
        "2024-11-05",
    ];
}
