namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// 协议版本信息
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
    /// 从字符串隐式转换为 <see cref="ProtocolVersion"/>。
    /// </summary>
    public static implicit operator ProtocolVersion(string value)
    {
        return new ProtocolVersion(value);
    }

    /// <summary>
    /// 从 <see cref="ProtocolVersion"/> 隐式转换为字符串。
    /// </summary>
    public static implicit operator string(ProtocolVersion version)
    {
        return version.ToString();
    }

    /// <summary>
    /// 比较两个协议版本，判断左侧是否大于右侧。
    /// </summary>
    public static bool operator >(ProtocolVersion left, ProtocolVersion right)
    {
        return string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal) > 0;
    }

    /// <summary>
    /// 比较两个协议版本，判断左侧是否小于右侧。
    /// </summary>
    public static bool operator <(ProtocolVersion left, ProtocolVersion right)
    {
        return string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal) < 0;
    }

    private const string CurrentVersion = "2025-11-25";
    private const string MinimumVersion = "2024-11-05";
    private const string StreamableHttpMinimumVersion = "2025-03-26";

    /// <summary>
    /// 当前使用的协议版本
    /// </summary>
    public static readonly ProtocolVersion Current = new(CurrentVersion);

    /// <summary>
    /// 所有已知协议版本中的最低版本
    /// </summary>
    public static readonly ProtocolVersion Minimum = new(MinimumVersion);

    /// <summary>
    /// Streamable HTTP 传输层所需的最低协议版本（2025-03-26 引入 Streamable HTTP）
    /// </summary>
    public static readonly ProtocolVersion StreamableHttpMinimum = new(StreamableHttpMinimumVersion);

    /// <summary>
    /// 历史版本列表，按时间倒序排列
    /// </summary>
    internal static IReadOnlyList<string> HistoryVersions { get; } =
    [
        "2025-11-25",
        "2025-06-18",
        "2025-03-26",
        "2024-11-05",
    ];
}
