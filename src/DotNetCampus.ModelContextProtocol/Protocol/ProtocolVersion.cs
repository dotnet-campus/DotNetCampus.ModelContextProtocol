namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// 协议版本信息<br/>
/// Protocol version information
/// </summary>
internal readonly record struct ProtocolVersion
{
    private readonly string? _value;

    private ProtocolVersion(string value)
    {
        _value = value;
    }

    public override string ToString()
    {
        return _value ?? MinimumVersion;
    }

    public static implicit operator ProtocolVersion(string value)
    {
        return new ProtocolVersion(value);
    }

    public static implicit operator string(ProtocolVersion version)
    {
        return version.ToString();
    }

    public static bool operator >(ProtocolVersion left, ProtocolVersion right)
    {
        return string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal) > 0;
    }

    public static bool operator <(ProtocolVersion left, ProtocolVersion right)
    {
        return string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal) < 0;
    }

    private const string CurrentVersion = "2025-11-25";
    private const string MinimumVersion = "2024-11-05";

    /// <summary>
    /// 当前使用的协议版本<br/>
    /// The currently used protocol version
    /// </summary>
    public static readonly ProtocolVersion Current = new(CurrentVersion);

    /// <summary>
    /// 大多数功能正常运行所需的最低版本<br/>
    /// The minimum version required for most features to work properly
    /// </summary>
    public static readonly ProtocolVersion Minimum = new(MinimumVersion);

    /// <summary>
    /// 历史版本列表，按时间倒序排列<br/>
    /// List of historical versions, sorted in reverse chronological order
    /// </summary>
    internal static IReadOnlyList<string> HistoryVersions { get; } =
    [
        "2025-11-25",
        "2025-06-18",
        "2025-03-26",
        "2024-11-05",
    ];
}
