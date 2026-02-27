namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// 协议版本信息<br/>
/// Protocol version information
/// </summary>
public readonly record struct ProtocolVersion
{
    private readonly string? _value;

    private ProtocolVersion(string value)
    {
        _value = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return _value ?? MinimumVersion;
    }

    /// <summary>
    /// 从字符串隐式转换为 <see cref="ProtocolVersion"/>。<br/>
    /// Implicitly converts a string to <see cref="ProtocolVersion"/>.
    /// </summary>
    public static implicit operator ProtocolVersion(string value)
    {
        return new ProtocolVersion(value);
    }

    /// <summary>
    /// 从 <see cref="ProtocolVersion"/> 隐式转换为字符串。<br/>
    /// Implicitly converts a <see cref="ProtocolVersion"/> to string.
    /// </summary>
    public static implicit operator string(ProtocolVersion version)
    {
        return version.ToString();
    }

    /// <summary>
    /// 比较两个协议版本，判断左侧是否大于右侧。<br/>
    /// Compares two protocol versions to determine if left is greater than right.
    /// </summary>
    public static bool operator >(ProtocolVersion left, ProtocolVersion right)
    {
        return string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal) > 0;
    }

    /// <summary>
    /// 比较两个协议版本，判断左侧是否小于右侧。<br/>
    /// Compares two protocol versions to determine if left is less than right.
    /// </summary>
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
